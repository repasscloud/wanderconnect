#!/usr/bin/env python3
"""One-shot UAT operator dispatcher. Private runtime inputs only; no serving or automatic fallback."""
import os,subprocess,sys,base64,hashlib,hmac,secrets,re,tempfile,zipfile,urllib.request,urllib.parse,time
from pathlib import Path
BACKUPS=Path('/opt/backups')
def required(name):
    value=os.environ.get(name,'')
    if not value.strip():raise ValueError('Required private runtime input is unavailable.')
    return value

def run(values):
    return subprocess.run(values,check=True).returncode

def identifier(value):
    if not re.fullmatch('[a-z][a-z0-9_]{0,62}',value):raise ValueError('Invalid database administrator identifier.')
    return '"'+value+'"'

def verifier(password):
    if len(password)<32 or len(password)>1024 or any(ord(c)<33 or ord(c)>126 for c in password):raise ValueError('Database passwords require32–1024 printable ASCII characters without spaces.')
    salt=secrets.token_bytes(16);salted=hashlib.pbkdf2_hmac('sha256',password.encode(),salt,4096)
    client=hmac.digest(salted,b'Client Key','sha256');stored=hashlib.sha256(client).digest();server=hmac.digest(salted,b'Server Key','sha256')
    enc=lambda value:base64.b64encode(value).decode()
    return 'SCRAM-SHA-256$4096:'+enc(salt)+'$'+enc(stored)+':'+enc(server)

def database_statements(env):
    admin=identifier(env['SWOOVR_PG_ADMIN'])
    roles={'swoovr_api':'SWOOVR_API_DB_PASSWORD','swoovr_migrator':'SWOOVR_MIGRATOR_DB_PASSWORD','swoovr_operator':'SWOOVR_OPERATOR_DB_PASSWORD','swoovr_backup':'SWOOVR_BACKUP_DB_PASSWORD','swoovr_keycloak':'KC_DB_PASSWORD'}
    setup=['SET password_encryption=\'scram-sha-256\';']
    for role,key in roles.items():
        password=verifier(env[key])
        setup += [f"DO $swoovr$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='{role}') THEN CREATE ROLE {role}; END IF; END $swoovr$;",f"DO $swoovr$ BEGIN IF EXISTS(SELECT FROM pg_roles WHERE rolname='{role}' AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN RAISE EXCEPTION 'Refusing privileged application role'; END IF; END $swoovr$;",f"ALTER ROLE {role} LOGIN PASSWORD '{password}';"]
    setup += [f'GRANT swoovr_migrator,swoovr_keycloak TO {admin};','ALTER DATABASE swoovr OWNER TO swoovr_migrator;','ALTER DATABASE keycloak OWNER TO swoovr_keycloak;','REVOKE ALL ON DATABASE swoovr,keycloak FROM PUBLIC;','GRANT CONNECT ON DATABASE swoovr TO swoovr_api,swoovr_operator,swoovr_backup;','GRANT CONNECT ON DATABASE keycloak TO swoovr_backup;']
    application=['REVOKE CREATE ON SCHEMA public FROM PUBLIC;','CREATE SCHEMA IF NOT EXISTS restricted AUTHORIZATION swoovr_migrator;']
    for schema in ['public','restricted']:
        application += [f'GRANT USAGE ON SCHEMA {schema} TO swoovr_api,swoovr_operator,swoovr_backup;',f'GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA {schema} TO swoovr_api,swoovr_operator;',f'GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA {schema} TO swoovr_api,swoovr_operator;',f'GRANT SELECT ON ALL TABLES IN SCHEMA {schema} TO swoovr_backup;',f'GRANT SELECT ON ALL SEQUENCES IN SCHEMA {schema} TO swoovr_backup;',f'ALTER DEFAULT PRIVILEGES FOR ROLE swoovr_migrator IN SCHEMA {schema} GRANT SELECT,INSERT,UPDATE,DELETE ON TABLES TO swoovr_api,swoovr_operator;',f'ALTER DEFAULT PRIVILEGES FOR ROLE swoovr_migrator IN SCHEMA {schema} GRANT USAGE,SELECT ON SEQUENCES TO swoovr_api,swoovr_operator;',f'ALTER DEFAULT PRIVILEGES FOR ROLE swoovr_migrator IN SCHEMA {schema} GRANT SELECT ON TABLES TO swoovr_backup;',f'ALTER DEFAULT PRIVILEGES FOR ROLE swoovr_migrator IN SCHEMA {schema} GRANT SELECT ON SEQUENCES TO swoovr_backup;']
    identity=['REVOKE CREATE ON SCHEMA public FROM PUBLIC;','GRANT USAGE ON SCHEMA public TO swoovr_backup;','GRANT SELECT ON ALL TABLES IN SCHEMA public TO swoovr_backup;','GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO swoovr_backup;','ALTER DEFAULT PRIVILEGES FOR ROLE swoovr_keycloak IN SCHEMA public GRANT SELECT ON TABLES TO swoovr_backup;','ALTER DEFAULT PRIVILEGES FOR ROLE swoovr_keycloak IN SCHEMA public GRANT SELECT ON SEQUENCES TO swoovr_backup;']
    return {'postgres':setup,'swoovr':application,'keycloak':identity}

def database_bootstrap():
    if os.environ.get('DOTNET_ENVIRONMENT')!='Development' and (required('PGSSLMODE')!='verify-full' or not Path(required('PGSSLROOTCERT')).is_file()):raise ValueError('Verified database TLS is required.')
    for database,statements in database_statements(os.environ).items():
        # No password values in argv, files or emitted diagnostics. DDL uses SCRAM verifiers.
        result=subprocess.run(['psql','-X','--no-psqlrc','--set','ON_ERROR_STOP=1','--dbname',database],input='BEGIN;\n'+'\n'.join(statements)+'\nCOMMIT;\n',text=True,capture_output=True,timeout=120)
        if result.returncode:raise ValueError('Database bootstrap refused or failed.')
    print('Database roles/ownership/default privileges prepared. Run migration before serving; hosted permission acceptance remains separate.')
    return 0

def restore_permission_sql(target,kind):
    if not re.fullmatch(r'swoovr_restore_[0-9a-f]{32}',target) or kind not in ('api','keycloak'):raise ValueError('Owned restore target required.')
    owner='swoovr_migrator' if kind=='api' else 'swoovr_keycloak'
    schemas=['public','restricted'] if kind=='api' else ['public']
    schema_list=','.join("'"+v+"'" for v in schemas)
    commands=[f'ALTER DATABASE {target} OWNER TO {owner};',f'REVOKE ALL ON DATABASE {target} FROM PUBLIC;',f'GRANT CONNECT ON DATABASE {target} TO '+('swoovr_api,swoovr_operator,swoovr_backup' if kind=='api' else 'swoovr_keycloak,swoovr_backup')+';']
    for schema in schemas:commands.append(f'ALTER SCHEMA {schema} OWNER TO {owner};')
    commands.append(f"""DO $repair$ DECLARE item record; BEGIN
      IF EXISTS(SELECT FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname IN ({schema_list})) THEN RAISE EXCEPTION 'Custom routines require explicit ownership review'; END IF;
      FOR item IN SELECT n.nspname,c.relname,c.relkind FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ({schema_list}) AND c.relkind IN ('r','p','S','v','m') LOOP
        EXECUTE format('ALTER %s %I.%I OWNER TO %I',CASE item.relkind WHEN 'S' THEN 'SEQUENCE' WHEN 'v' THEN 'VIEW' WHEN 'm' THEN 'MATERIALIZED VIEW' ELSE 'TABLE' END,item.nspname,item.relname,'{owner}');
      END LOOP;
      FOR item IN SELECT n.nspname,t.typname,t.typtype FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace WHERE n.nspname IN ({schema_list}) AND t.typtype IN ('e','d') LOOP
        EXECUTE format('ALTER %s %I.%I OWNER TO %I',CASE item.typtype WHEN 'd' THEN 'DOMAIN' ELSE 'TYPE' END,item.nspname,item.typname,'{owner}');
      END LOOP;
    END $repair$;""")
    for schema in schemas:
        for actors,table,sequence in ([('swoovr_api,swoovr_operator','SELECT,INSERT,UPDATE,DELETE','USAGE,SELECT'),('swoovr_backup','SELECT','SELECT')] if kind=='api' else [('swoovr_backup','SELECT','SELECT')]):
            commands += [f'GRANT USAGE ON SCHEMA {schema} TO {actors};',f'GRANT {table} ON ALL TABLES IN SCHEMA {schema} TO {actors};',f'GRANT {sequence} ON ALL SEQUENCES IN SCHEMA {schema} TO {actors};',f'ALTER DEFAULT PRIVILEGES FOR ROLE {owner} IN SCHEMA {schema} GRANT {table} ON TABLES TO {actors};',f'ALTER DEFAULT PRIVILEGES FOR ROLE {owner} IN SCHEMA {schema} GRANT {sequence} ON SEQUENCES TO {actors};']
    if kind=='api':commands+=['GRANT USAGE ON SCHEMA swoovr_operations TO swoovr_api;','GRANT SELECT ON swoovr_operations.restore_guard TO swoovr_api;']
    return 'BEGIN;\n'+'\n'.join(commands)+'\nCOMMIT;\n'

def recovery_permissions(api,kc):
    if api==kc:raise ValueError('Distinct paired restore targets required.')
    for target,kind in [(api,'api'),(kc,'keycloak')]:restore_permission_sql(target,kind)
    if os.environ.get('DOTNET_ENVIRONMENT')!='Development' and (required('PGSSLMODE')!='verify-full' or not Path(required('PGSSLROOTCERT')).is_file()):raise ValueError('Verified database TLS required.')
    import json
    receipts=[]
    for target,kind in [(api,'api'),(kc,'keycloak')]:
        result=subprocess.run(['psql','-X','-tA','--set','ON_ERROR_STOP=1','--dbname',target],input='SELECT row_to_json(g) FROM swoovr_operations.restore_guard g WHERE id=1;',capture_output=True,text=True,timeout=30)
        if result.returncode:raise ValueError('Paired readiness unavailable.')
        receipt=json.loads(result.stdout)
        if receipt.get('ready') is not True or receipt.get('role')!=kind or not re.fullmatch('[0-9a-f]{32}',receipt.get('operation_id','')) or not re.fullmatch('[0-9a-f]{64}',receipt.get('authority_sha256','')):raise ValueError('Paired reconciled readiness required.')
        receipts.append(receipt)
    if receipts[0]['operation_id']!=receipts[1]['operation_id']:raise ValueError('Paired operation mismatch.')
    for target,kind in [(api,'api'),(kc,'keycloak')]:
        result=subprocess.run(['psql','-X','--set','ON_ERROR_STOP=1','--dbname',target],input=restore_permission_sql(target,kind),capture_output=True,text=True,timeout=120)
        if result.returncode:raise ValueError('Restore permissions failed; keep ingress disabled and inspect privately.')
    print('Paired target ownership/data permissions repaired; operator-owned fences preserved. Hosted readiness/smoke and promotion approval remain required.')
    return 0

class NoDownloadRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None

def places_import():
    # Public reference data only, fixed HTTPS source and bounded/private ephemeral files.
    with tempfile.TemporaryDirectory(prefix='swoovr-geonames-') as temporary:
        folder=Path(temporary);os.chmod(folder,0o700)
        for name in ['cities1000.zip','admin1CodesASCII.txt','countryInfo.txt']:
            target=folder/name;size=0;deadline=time.monotonic()+120
            request=urllib.request.Request('https://download.geonames.org/export/dump/'+name)
            with urllib.request.build_opener(urllib.request.ProxyHandler({}),NoDownloadRedirect()).open(request,timeout=30) as response,target.open('wb') as output:
                if urllib.parse.urlsplit(response.url).hostname!='download.geonames.org':raise ValueError('Unexpected place-data source.')
                os.chmod(target,0o600)
                while chunk:=response.read(65536):
                    size+=len(chunk)
                    if size>64*1024*1024 or time.monotonic()>deadline:raise ValueError('Place download exceeded its bound.')
                    output.write(chunk)
            print('Public GeoNames input '+name+' sha256='+hashlib.sha256(target.read_bytes()).hexdigest(),flush=True)
        with zipfile.ZipFile(folder/'cities1000.zip') as archive:
            member=archive.getinfo('cities1000.txt')
            if member.file_size>256*1024*1024:raise ValueError('Place archive exceeded its bound.')
            target=folder/'cities1000.txt'
            with archive.open(member) as source,target.open('wb') as output:
                os.chmod(target,0o600)
                import shutil
                shutil.copyfileobj(source,output)
        return run(['dotnet','/app/Swoovr.Api.dll','geonames','import',str(folder)])

def main(args):
    if len(args)==3 and args[0]=='recovery-permissions':return recovery_permissions(args[1],args[2])
    if args==['places-import']:return places_import()
    if args==['database-bootstrap']:return database_bootstrap()
    if args==['keycloak-bootstrap']:
        from keycloak_bootstrap import main as bootstrap
        return bootstrap()
    if args==['tools-check']:
        for values in [['pg_dump','--version'],['pg_restore','--version'],['psql','--version'],['age','--version'],['dotnet','--list-runtimes']]:run(values)
        import azure.identity,azure.storage.blob
        print('PASS operator tooling imports; no connection or provider request.');return 0
    if args==['recovery-help']:
        print('Recovery is manual and fenced. Follow the paired restore/reconciliation drill before any ingress activation. No restoration performed.');return 0
    if args in (['backup','api'],['backup','keycloak'],['backup-expiry']):
        common=['--account-url',required('SWOOVR_BACKUP_ACCOUNT'),'--container',required('SWOOVR_BACKUP_CONTAINER'),'--client-id',required('AZURE_CLIENT_ID')]
        if args==['backup-expiry']:return run([sys.executable,str(BACKUPS/'azure_store.py'),*common,'expire'])
        command=[sys.executable,str(BACKUPS/'scheduled.py'),*common,'--role',args[1],'--recipient',required('SWOOVR_BACKUP_RECIPIENT')]
        if args[1]=='api':command+=['--bundle-producer','/opt/uat/bundle-producer']
        return run(command)
    raise ValueError('Unsupported or incomplete operator command.')
if __name__=='__main__':
    try:raise SystemExit(main(sys.argv[1:]))
    except (ValueError,KeyError,OSError,zipfile.BadZipFile,subprocess.SubprocessError):raise SystemExit('Operator command failed; no success or fallback. Review private job diagnostics before retrying.')
