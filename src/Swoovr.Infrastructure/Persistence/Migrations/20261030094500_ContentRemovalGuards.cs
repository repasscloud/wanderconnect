using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Swoovr.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SwoovrDbContext))]
[Migration("20261030094500_ContentRemovalGuards")]
public sealed class ContentRemovalGuards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION content_removal_content_disposal_safe(source uuid) RETURNS boolean LANGUAGE sql VOLATILE AS $$
          SELECT EXISTS(SELECT 1 FROM moderation_cases c WHERE c.id=source AND c.status='Closed'
            AND c.closed_at IS NOT NULL AND c.evidence_purged_at IS NOT NULL
            AND NOT EXISTS(SELECT 1 FROM appeals a JOIN sanctions s ON s.id=a.sanction_id WHERE s.case_id=c.id AND a.status='Pending')
            AND NOT EXISTS(SELECT 1 FROM legal_holds h
              WHERE (h.status<>'Released' OR EXISTS(SELECT 1 FROM evidence_items e WHERE e.hold_id=h.id))
              AND (h.related_account_ids IS NULL OR h.retention_case_ids IS NULL OR h.case_id=c.id OR c.id=ANY(h.retention_case_ids)
                OR EXISTS(SELECT 1 FROM (
                  SELECT c.subject_account_id AS account_id UNION SELECT r.reporter_account_id FROM reports r WHERE r.case_id=c.id
                ) people WHERE people.account_id=ANY(h.related_account_ids)
                  OR EXISTS(SELECT 1 FROM legal_hold_scopes scope WHERE scope.hold_id=h.id AND scope.kind='Account' AND scope.target_id=people.account_id)
                  OR EXISTS(SELECT 1 FROM evidence_items e WHERE e.hold_id=h.id AND e.account_id=people.account_id)))))
        $$;
        CREATE FUNCTION content_removal_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='TRUNCATE' THEN RAISE EXCEPTION 'Content removal operation refused'; END IF;
          IF TG_OP='DELETE' THEN
            IF OLD.sealed_snapshot IS NOT NULL OR OLD.evidence_disposed_at IS NULL OR NOT ban_case_disposal_safe(OLD.case_id)
              THEN RAISE EXCEPTION 'Content removal operation refused'; END IF;
            RETURN OLD;
          END IF;
          IF TG_OP='UPDATE' THEN
            IF ROW(NEW.id,NEW.case_id,NEW.report_id,NEW.subject_account_id,NEW.actor_account_id,NEW.field,NEW.mode_id,NEW.message_id,NEW.removed_at)
              IS DISTINCT FROM ROW(OLD.id,OLD.case_id,OLD.report_id,OLD.subject_account_id,OLD.actor_account_id,OLD.field,OLD.mode_id,OLD.message_id,OLD.removed_at)
              THEN RAISE EXCEPTION 'Content removal operation refused'; END IF;
            IF NEW.snapshot_hash IS NOT DISTINCT FROM OLD.snapshot_hash AND NEW.sealed_snapshot IS NOT DISTINCT FROM OLD.sealed_snapshot AND NEW.evidence_disposed_at IS NOT DISTINCT FROM OLD.evidence_disposed_at THEN RETURN NEW; END IF;
            IF OLD.sealed_snapshot IS NOT NULL AND NEW.sealed_snapshot IS NOT NULL
              AND OLD.evidence_disposed_at IS NULL AND NEW.evidence_disposed_at IS NULL AND NEW.snapshot_hash IS NOT DISTINCT FROM OLD.snapshot_hash
              AND octet_length(NEW.sealed_snapshot)>=29 AND get_byte(NEW.sealed_snapshot,0)>0
              AND get_byte(NEW.sealed_snapshot,0)<>get_byte(OLD.sealed_snapshot,0) THEN RETURN NEW; END IF;
            IF OLD.sealed_snapshot IS NOT NULL AND OLD.evidence_disposed_at IS NULL AND NEW.sealed_snapshot IS NULL
              AND octet_length(NEW.snapshot_hash)=0 AND NEW.evidence_disposed_at IS NOT NULL AND NEW.evidence_disposed_at>=OLD.removed_at
              AND NEW.evidence_disposed_at<=clock_timestamp()+interval '5 seconds'
              AND current_setting('transaction_isolation')='read committed'
              AND content_removal_content_disposal_safe(OLD.case_id) THEN RETURN NEW; END IF;
            RAISE EXCEPTION 'Content removal operation refused';
          END IF;
          IF NEW.id='00000000-0000-0000-0000-000000000000'::uuid
            OR NEW.actor_account_id='00000000-0000-0000-0000-000000000000'::uuid
            OR NEW.sealed_snapshot IS NULL OR NEW.evidence_disposed_at IS NOT NULL
            OR octet_length(NEW.snapshot_hash)<>32 OR octet_length(NEW.sealed_snapshot)<29 OR get_byte(NEW.sealed_snapshot,0)=0
            OR NOT EXISTS(SELECT 1 FROM moderation_cases c JOIN reports r ON r.case_id=c.id
              WHERE c.id=NEW.case_id AND r.id=NEW.report_id AND c.subject_account_id=NEW.subject_account_id AND r.subject_account_id=NEW.subject_account_id
                AND c.evidence_purged_at IS NULL
                AND ((NEW.field='about' AND NEW.mode_id IS NULL AND NEW.message_id IS NULL AND r.subject_type IN ('Profile','Bot'))
                  OR (NEW.field='mode-about' AND NEW.mode_id IN ('dating','friends','activities','business') AND NEW.message_id IS NULL AND r.subject_type IN ('Profile','Bot'))
                  OR (NEW.field='message' AND NEW.mode_id IS NULL AND NEW.message_id IS NOT NULL AND r.subject_type IN ('Message','Conversation')
                    AND EXISTS(SELECT 1 FROM messages m WHERE m.id=NEW.message_id AND m.sender_account_id=NEW.subject_account_id AND m.kind='Text'))))
            THEN RAISE EXCEPTION 'Content removal operation refused'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER content_removal_authority BEFORE INSERT OR UPDATE OR DELETE ON content_removal
          FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
        CREATE TRIGGER content_removal_guard BEFORE INSERT OR UPDATE OR DELETE ON content_removal
          FOR EACH ROW EXECUTE FUNCTION content_removal_guard();
        CREATE TRIGGER content_removal_no_truncate BEFORE TRUNCATE ON content_removal
          FOR EACH STATEMENT EXECUTE FUNCTION content_removal_guard();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER content_removal_no_truncate ON content_removal;
        DROP TRIGGER content_removal_guard ON content_removal;
        DROP TRIGGER content_removal_authority ON content_removal;
        DROP FUNCTION content_removal_guard();
        DROP FUNCTION content_removal_content_disposal_safe(uuid);
        """);
}
