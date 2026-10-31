#!/usr/bin/env python3
"""One-shot UAT operator dispatcher. Private runtime inputs only; no serving or automatic fallback."""
import os,subprocess,sys,base64,hashlib,hmac,secrets,re
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

def main(args):
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
    except (ValueError,KeyError,OSError,subprocess.SubprocessError):raise SystemExit('Operator command failed; no success or fallback. Review private job diagnostics before retrying.')
