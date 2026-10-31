#!/usr/bin/env python3
"""Owned PostgreSQL fixture for role separation; fictional generated secrets, no MAIN or Azure."""
import importlib.util,os,secrets,subprocess,tempfile,time,uuid,base64,io,tarfile,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('operator_job',ROOT/'scripts/uat/uat_jobs.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
name='swoovr-uat-db-'+uuid.uuid4().hex[:12];checks=0

def docker(*args,input=None,check=True):return subprocess.run(['docker',*args],input=input,text=True,capture_output=True,check=check,timeout=120)
def sql(database,text,role='postgres',check=True):
    return docker('exec','-i',name,'psql','-X','-v','ON_ERROR_STOP=1','-U',role,'-d',database,input=text,check=check)
with tempfile.TemporaryDirectory(prefix='swoovr-db-bootstrap-') as private:
    directory=Path(private);os.chmod(directory,0o700)
    values={k:secrets.token_urlsafe(40) for k in ['SWOOVR_API_DB_PASSWORD','SWOOVR_MIGRATOR_DB_PASSWORD','SWOOVR_OPERATOR_DB_PASSWORD','SWOOVR_BACKUP_DB_PASSWORD','KC_DB_PASSWORD']};values['SWOOVR_PG_ADMIN']='swoovrbootstrap'
    env=directory/'postgres.env';env.write_text('POSTGRES_PASSWORD='+secrets.token_urlsafe(40)+'\n'+'\n'.join(k+'='+v for k,v in values.items())+'\n');os.chmod(env,0o600)
    try:
        docker('run','-d','--name',name,'--network','none','--env-file',str(env),'postgres:17.11-alpine@sha256:eee6c6e1b0894fa4fe0075eb054af9fd1cf39a35321c2d4110626cfe506411fb')
        for _ in range(60):
            if docker('exec',name,'pg_isready','-U','postgres',check=False).returncode==0:break
            time.sleep(1)
        else:raise AssertionError('Owned PostgreSQL unavailable.')
        admin_password=secrets.token_urlsafe(40)
        sql('postgres',"CREATE ROLE swoovrbootstrap LOGIN CREATEROLE CREATEDB PASSWORD '"+m.verifier(admin_password)+"';")
        sql('postgres','CREATE DATABASE swoovr;','swoovrbootstrap');sql('postgres','CREATE DATABASE keycloak;','swoovrbootstrap')
        bootstrap=directory/'bootstrap.env';bootstrap.write_text('DOTNET_ENVIRONMENT=Development\nPGSSLMODE=disable\nPGHOST=127.0.0.1\nPGUSER=swoovrbootstrap\nPGPASSWORD='+admin_password+'\n'+'\n'.join(k+'='+v for k,v in values.items())+'\n');os.chmod(bootstrap,0o600)
        booted=docker('run','--rm','--platform','linux/amd64','--network','container:'+name,'--env-file',str(bootstrap),'swoovr-uat-operator:phase-zero-check','database-bootstrap',check=False)
        assert booted.returncode==0,'Published bootstrap dispatcher failed under non-superuser administrator.';checks+=1
        statements=m.database_statements(values)
        for repeat in range(2):
            for database,commands in statements.items():sql(database,'BEGIN;\n'+'\n'.join(commands)+'\nCOMMIT;','swoovrbootstrap');checks+=1
        for role,key in [('swoovr_api','SWOOVR_API_DB_PASSWORD'),('swoovr_migrator','SWOOVR_MIGRATOR_DB_PASSWORD'),('swoovr_operator','SWOOVR_OPERATOR_DB_PASSWORD'),('swoovr_backup','SWOOVR_BACKUP_DB_PASSWORD'),('swoovr_keycloak','KC_DB_PASSWORD')]:
            database='keycloak' if role=='swoovr_keycloak' else 'swoovr'
            command='PGPASSWORD="$'+key+'" psql -X -h 127.0.0.1 -U '+role+' -d '+database+' -v ON_ERROR_STOP=1 -c "SELECT current_user"'
            result=docker('exec',name,'sh','-ec',command);assert role in result.stdout;checks+=1
        migration=directory/'migration.env';migration.write_text('DOTNET_ENVIRONMENT=Uat\nASPNETCORE_ENVIRONMENT=Uat\nEmail__Provider=disabled\nPush__Provider=disabled\nMedia__Store=none\nConnectionStrings__Swoovr=Host=127.0.0.1;Database=swoovr;Username=swoovr_migrator;Password='+values['SWOOVR_MIGRATOR_DB_PASSWORD']+';SSL Mode=Disable\n');os.chmod(migration,0o600)
        migrated=docker('run','--rm','--platform','linux/amd64','--network','container:'+name,'--env-file',str(migration),'--entrypoint','dotnet','swoovr-uat-operator:phase-zero-check','/app/Swoovr.Api.dll','migrate',check=False)
        assert migrated.returncode==0,'Migration under the separate schema owner failed.';checks+=1
        sql('swoovr','SELECT count(*) FROM public.accounts;','swoovr_api');checks+=1
        sql('swoovr','SELECT count(*) FROM public.accounts;','swoovr_backup');checks+=1
        sql('swoovr','CREATE TABLE public.fictional_profile(id int); CREATE TABLE restricted.fictional_evidence(id int);','swoovr_migrator');checks+=1
        for role in ['swoovr_api','swoovr_operator']:
            sql('swoovr','INSERT INTO public.fictional_profile VALUES(1); INSERT INTO restricted.fictional_evidence VALUES(1);',role);checks+=1
            assert sql('swoovr','CREATE TABLE public.forbidden(id int);',role,False).returncode!=0;checks+=1
            assert sql('postgres','CREATE ROLE forbidden;',role,False).returncode!=0;checks+=1
        sql('swoovr','SELECT * FROM public.fictional_profile; SELECT * FROM restricted.fictional_evidence;','swoovr_backup');checks+=1
        assert sql('swoovr','DELETE FROM public.fictional_profile;','swoovr_backup',False).returncode!=0;checks+=1
        sql('keycloak','CREATE TABLE public.fictional_identity(id int);','swoovr_keycloak');checks+=1
        sql('keycloak','SELECT * FROM public.fictional_identity;','swoovr_backup');checks+=1
        assert sql('keycloak','SELECT 1;','swoovr_api',False).returncode!=0;checks+=1
        assert sql('swoovr','SELECT 1;','swoovr_keycloak',False).returncode!=0;checks+=1
        backup=directory/'backup.env';backup.write_text(migration.read_text().replace('Username=swoovr_migrator;Password='+values['SWOOVR_MIGRATOR_DB_PASSWORD'],'Username=swoovr_backup;Password='+values['SWOOVR_BACKUP_DB_PASSWORD'])+'Founders__Secrets__1='+secrets.token_urlsafe(40)+'\nEvidence__Keys__1='+base64.b64encode(secrets.token_bytes(32)).decode()+'\n');os.chmod(backup,0o600)
        captured=subprocess.run(['docker','run','--rm','--platform','linux/amd64','--network','container:'+name,'--env-file',str(backup),'--entrypoint','/opt/uat/bundle-producer','swoovr-uat-operator:phase-zero-check'],capture_output=True,timeout=120)
        assert captured.returncode==0,'Actual published API snapshot bundle failed under the read-only backup login.';checks+=1
        with tarfile.open(fileobj=io.BytesIO(captured.stdout)) as archive:
            assert set(archive.getnames())=={'database.dump','manifest.json'};checks+=1
            dump=archive.extractfile('database.dump').read();manifest=json.load(archive.extractfile('manifest.json'))
            assert dump.startswith(b'PGDMP') and manifest['databaseSha256']==hashlib.sha256(dump).hexdigest() and manifest['objects']==[];checks+=1
        sql('postgres','ALTER ROLE swoovr_backup SUPERUSER;')
        refused=sql('postgres','BEGIN;\n'+'\n'.join(m.database_statements(values)['postgres'])+'\nCOMMIT;','swoovrbootstrap',False)
        assert refused.returncode!=0 and 'Refusing privileged application role' in refused.stderr;checks+=1
        for key,value in values.items():
            if key=='SWOOVR_PG_ADMIN':continue
            assert value not in '\n'.join(sum(statements.values(),[]));checks+=1
        print(f'PASS {checks} owned PostgreSQL bootstrap/idempotence/SCRAM login/role-permission checks. Local database only; no Azure TLS or managed-service acceptance.')
    finally:
        docker('rm','-f','-v',name,check=False)
        print('CLEANUP owned database and private fixtures; MAIN untouched.')
