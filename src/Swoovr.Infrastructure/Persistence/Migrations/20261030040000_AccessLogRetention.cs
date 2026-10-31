using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Swoovr.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SwoovrDbContext))]
[Migration("20261030040000_AccessLogRetention")]
public sealed class AccessLogRetention:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE sensitive_access_checkpoint (
          id integer PRIMARY KEY CHECK(id=1), sequence bigint NOT NULL DEFAULT 0,
          hash bytea NOT NULL DEFAULT ''::bytea, through_at timestamptz NULL, disposed_at timestamptz NULL,
          permit_xid xid8 NULL, permit_through bigint NULL,
          CHECK(sequence>=0 AND (octet_length(hash)=32 OR sequence=0 AND octet_length(hash)=0)));
        INSERT INTO sensitive_access_checkpoint(id) VALUES(1);
        REVOKE INSERT,UPDATE,DELETE,TRUNCATE ON sensitive_access_checkpoint FROM PUBLIC;
        CREATE OR REPLACE FUNCTION sensitive_access_log_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' AND EXISTS(SELECT 1 FROM sensitive_access_checkpoint c
            WHERE c.id=1 AND c.permit_xid=pg_current_xact_id() AND c.permit_through>=OLD.sequence) THEN RETURN OLD; END IF;
          RAISE EXCEPTION 'sensitive_access_log is append-only outside verified prefix expiry';
        END $$;
        CREATE FUNCTION swoovr_expire_access_prefix(at_time timestamptz) RETURNS integer LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp SET timezone = 'UTC' AS $$
        DECLARE checkpoint sensitive_access_checkpoint%ROWTYPE; entry sensitive_access_log%ROWTYPE;
          previous bytea; last_sequence bigint; last_at timestamptz; amount integer:=0; expected bytea;
        BEGIN
          IF at_time IS NULL OR at_time>clock_timestamp()+interval '5 seconds' THEN RAISE EXCEPTION 'Future expiry authority refused'; END IF;
          PERFORM pg_advisory_xact_lock(1347568979,0);
          PERFORM pg_advisory_xact_lock(6005355830291090503);
          SELECT * INTO STRICT checkpoint FROM sensitive_access_checkpoint WHERE id=1 FOR UPDATE;
          IF EXISTS(SELECT 1 FROM legal_holds)
            THEN RETURN 0; END IF;
          previous:=checkpoint.hash; last_sequence:=checkpoint.sequence;
          FOR entry IN SELECT * FROM sensitive_access_log ORDER BY sequence LIMIT 2000 LOOP
            IF entry.previous_hash<>previous OR entry.sequence<=last_sequence THEN RAISE EXCEPTION 'Audit chain boundary invalid'; END IF;
            expected:=sha256(previous||convert_to((floor(extract(epoch FROM entry.at)*1000)::bigint)::text||'|'||coalesce(entry.actor_account_id::text,'')||'|'||entry.action||'|'||entry.resource||'|'||coalesce(entry.resource_id,'')||'|'||entry.status::text,'UTF8'));
            IF entry.hash<>expected THEN RAISE EXCEPTION 'Audit chain hash invalid'; END IF;
            IF entry.at>at_time-interval '24 months'
              OR EXISTS(SELECT 1 FROM moderation_cases c WHERE c.created_at<=entry.at)
              OR EXISTS(SELECT 1 FROM restricted.le_cases c WHERE c.created_at<=entry.at)
              OR EXISTS(SELECT 1 FROM security_incidents s WHERE s.detected_at<=entry.at AND (s.status<>'Closed' OR s.closed_at IS NULL OR s.closed_at>at_time-interval '24 months'))
              THEN EXIT; END IF;
            previous:=entry.hash; last_sequence:=entry.sequence; last_at:=entry.at; amount:=amount+1;
          END LOOP;
          IF amount=0 THEN RETURN 0; END IF;
          UPDATE sensitive_access_checkpoint SET permit_xid=pg_current_xact_id(),permit_through=last_sequence WHERE id=1;
          DELETE FROM sensitive_access_log WHERE sequence<=last_sequence;
          UPDATE sensitive_access_checkpoint SET sequence=last_sequence,hash=previous,through_at=last_at,disposed_at=at_time,permit_xid=NULL,permit_through=NULL WHERE id=1;
          RETURN amount;
        END $$;
        REVOKE ALL ON FUNCTION swoovr_expire_access_prefix(timestamptz) FROM PUBLIC;
        CREATE FUNCTION swoovr_expire_operational_metadata(at_time timestamptz) RETURNS integer LANGUAGE plpgsql
          SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp SET timezone='UTC' AS $$
        DECLARE amount integer:=0; step integer;
        BEGIN
          IF at_time IS NULL OR at_time>clock_timestamp()+interval '5 seconds' THEN RAISE EXCEPTION 'Invalid expiry authority'; END IF;
          PERFORM pg_advisory_xact_lock(1347568979,0);
          IF EXISTS(SELECT 1 FROM legal_holds) THEN RETURN 0; END IF;
          DELETE FROM access_grants g WHERE g.id IN(SELECT a.id FROM access_grants a
            WHERE a.granted_at>a.expires_at-interval '1 day' AND a.expires_at<=at_time-interval '24 months'
              AND (a.revoked_at IS NULL OR a.revoked_at<=at_time-interval '24 months')
              AND NOT EXISTS(SELECT 1 FROM email_retirement_operations o WHERE o.access_grant_id=a.id)
              AND NOT EXISTS(SELECT 1 FROM email_retirement_reviews r WHERE r.access_grant_id=a.id)
              AND NOT EXISTS(SELECT 1 FROM sensitive_access_log l WHERE l.resource_id=a.id::text
                OR l.actor_account_id=a.account_id AND l.at BETWEEN a.granted_at AND a.expires_at)
              AND NOT EXISTS(SELECT 1 FROM moderation_cases c WHERE c.created_at<=a.expires_at)
              AND NOT EXISTS(SELECT 1 FROM restricted.le_cases c WHERE c.created_at<=a.expires_at)
            ORDER BY a.expires_at,a.id LIMIT 2000);
          GET DIAGNOSTICS step=ROW_COUNT; amount:=amount+step;
          DELETE FROM security_incidents i WHERE i.id IN(SELECT s.id FROM security_incidents s
            WHERE s.status='Closed' AND s.closed_at<=at_time-interval '24 months'
              AND s.closed_at>=s.detected_at AND s.assessed_at IS NOT NULL AND s.assessed_by IS NOT NULL AND s.closed_by IS NOT NULL
              AND s.notifiable IS NOT NULL AND s.assessed_at<=at_time-interval '24 months'
              AND (s.contained_at IS NULL OR s.contained_at<=at_time-interval '24 months')
              AND NOT EXISTS(SELECT 1 FROM moderation_cases c WHERE c.created_at<=greatest(s.closed_at,s.assessed_at,s.contained_at))
              AND NOT EXISTS(SELECT 1 FROM restricted.le_cases c WHERE c.created_at<=greatest(s.closed_at,s.assessed_at,s.contained_at))
            ORDER BY s.closed_at,s.id LIMIT 2000);
          GET DIAGNOSTICS step=ROW_COUNT; amount:=amount+step;
          DELETE FROM audit_entries a WHERE a.id IN(SELECT e.id FROM audit_entries e
            WHERE e.at<=at_time-interval '24 months' AND e.action NOT IN('staff-link.set','access.excluded','staff-conflict.preserved')
              AND NOT EXISTS(SELECT 1 FROM moderation_cases c WHERE c.created_at<=e.at+interval '1 day')
              AND NOT EXISTS(SELECT 1 FROM restricted.le_cases c WHERE c.created_at<=e.at+interval '1 day')
              AND NOT EXISTS(SELECT 1 FROM security_incidents s WHERE s.detected_at<=e.at+interval '1 day')
            ORDER BY e.id LIMIT 2000);
          GET DIAGNOSTICS step=ROW_COUNT; amount:=amount+step;
          IF amount>0 THEN
            INSERT INTO audit_entries(at,actor,action,target_type,detail) VALUES(at_time,'system','retention.operational-metadata-expired','retention',amount::text);
          END IF;
          RETURN amount;
        END $$;
        REVOKE ALL ON FUNCTION swoovr_expire_operational_metadata(timestamptz) FROM PUBLIC;

        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>throw new NotSupportedException("A disposed audit prefix cannot be reconstructed by a schema downgrade.");
}
