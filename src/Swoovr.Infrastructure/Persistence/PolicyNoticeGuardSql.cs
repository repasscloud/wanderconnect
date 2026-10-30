namespace Swoovr.Infrastructure.Persistence;
public static class PolicyNoticeGuardSql
{
    public const string Up="""
        CREATE FUNCTION public.swoovr_policy_authority() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
        BEGIN
            IF current_setting('transaction_isolation')<>'read committed' THEN RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy source writes require read committed';END IF;
            PERFORM pg_advisory_xact_lock_shared(1347568979,0);RETURN NULL;
        END $$;
        CREATE TRIGGER policy_publication_authority BEFORE INSERT OR UPDATE OR DELETE ON policy_notice_publications FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_policy_authority();
        CREATE TRIGGER policy_recipient_authority BEFORE INSERT OR UPDATE OR DELETE ON policy_notice_recipients FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_policy_authority();
        CREATE FUNCTION public.swoovr_policy_publication() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
        BEGIN
            IF TG_OP='DELETE' OR TG_OP='TRUNCATE' THEN RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy version dispositions cannot be removed';END IF;
            IF TG_OP='INSERT' THEN
                IF NEW.published_at IS NOT NULL OR NEW.disposed_at IS NOT NULL OR NEW.staged_by IS NULL OR NOT EXISTS(SELECT 1 FROM accounts a WHERE a.id=NEW.staged_by AND a.realm='Staff' AND a.status='Active' AND a.is_root_admin) THEN RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy source must be staged first';END IF;
                RETURN NEW;
            END IF;
            IF OLD.disposed_at IS NOT NULL OR NEW.id<>OLD.id OR NEW.version<>OLD.version OR NEW.staged_at<>OLD.staged_at THEN
                RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy source identity is immutable';
            END IF;
            IF NEW.disposed_at IS NOT NULL AND OLD.disposed_at IS NULL THEN
                IF coalesce(OLD.purge_after,OLD.staged_at+interval '1440 hours')>CURRENT_TIMESTAMP
                    OR EXISTS(SELECT 1 FROM policy_notice_recipients r WHERE r.publication_id=OLD.id)
                    OR EXISTS(SELECT 1 FROM notifications n WHERE n.kind='PolicyChange' AND n.subject_id=OLD.id)
                    OR EXISTS(SELECT 1 FROM evidence_items e WHERE e.kind='policy-notice' AND e.record_id LIKE OLD.id::text||'/%')
                    OR EXISTS(SELECT 1 FROM legal_holds h WHERE h.related_account_ids IS NULL OR h.retention_case_ids IS NULL)
                    OR NEW.published_at IS DISTINCT FROM OLD.published_at OR NEW.send_until IS DISTINCT FROM OLD.send_until OR NEW.purge_after IS DISTINCT FROM OLD.purge_after THEN
                    RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy group disposal authority unavailable';
                END IF;
                RETURN NEW;
            END IF;
            IF NEW.title<>OLD.title OR NEW.link<>OLD.link OR NEW.staged_by IS DISTINCT FROM OLD.staged_by
                OR (OLD.published_at IS NOT NULL AND (NEW.published_at IS DISTINCT FROM OLD.published_at OR NEW.published_by IS DISTINCT FROM OLD.published_by OR NEW.send_until IS DISTINCT FROM OLD.send_until OR NEW.purge_after IS DISTINCT FROM OLD.purge_after)) THEN
                RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy publication cannot be rewritten';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER policy_publication_guard BEFORE INSERT OR UPDATE OR DELETE ON policy_notice_publications FOR EACH ROW EXECUTE FUNCTION public.swoovr_policy_publication();
        CREATE TRIGGER policy_publication_no_truncate BEFORE TRUNCATE ON policy_notice_publications FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_policy_publication();
        CREATE FUNCTION public.swoovr_policy_recipient() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
        BEGIN
            IF TG_OP='TRUNCATE' THEN RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy recipients require grouped disposal';END IF;
            IF TG_OP='DELETE' THEN
                IF NOT EXISTS(SELECT 1 FROM policy_notice_publications p WHERE p.id=OLD.publication_id AND p.purge_after<=CURRENT_TIMESTAMP)
                    OR EXISTS(SELECT 1 FROM legal_holds h WHERE h.related_account_ids IS NULL OR h.retention_case_ids IS NULL OR (h.status<>'Released' AND (OLD.account_id=ANY(h.related_account_ids) OR EXISTS(SELECT 1 FROM legal_hold_scopes sc WHERE sc.hold_id=h.id AND sc.kind='Account' AND sc.target_id=OLD.account_id))))
                    OR EXISTS(SELECT 1 FROM evidence_items e WHERE e.kind='policy-notice' AND e.record_id LIKE OLD.publication_id::text||'/%') THEN
                    RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy recipient disposal authority unavailable';
                END IF;
                RETURN OLD;
            END IF;
            IF TG_OP='UPDATE' THEN
                IF NEW.publication_id<>OLD.publication_id OR NEW.account_id<>OLD.account_id OR NEW.created_at<>OLD.created_at OR OLD.status<>'queued' OR NEW.status NOT IN ('centre-recorded','suppressed') THEN
                    RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy delivery receipt is immutable';
                END IF;
                RETURN NEW;
            END IF;
            IF NEW.status<>'queued' OR NOT EXISTS(SELECT 1 FROM policy_notice_publications p JOIN accounts a ON a.id=NEW.account_id WHERE p.id=NEW.publication_id AND p.published_at IS NOT NULL AND p.disposed_at IS NULL AND a.realm='Members' AND a.created_at<=p.published_at) THEN
                RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy recipient source is invalid';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER policy_recipient_guard BEFORE INSERT OR UPDATE OR DELETE ON policy_notice_recipients FOR EACH ROW EXECUTE FUNCTION public.swoovr_policy_recipient();
        CREATE TRIGGER policy_recipient_no_truncate BEFORE TRUNCATE ON policy_notice_recipients FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_policy_recipient();
        """;
    public const string Down="""
        DROP TRIGGER policy_recipient_no_truncate ON policy_notice_recipients;
        DROP TRIGGER policy_recipient_guard ON policy_notice_recipients;
        DROP TRIGGER policy_recipient_authority ON policy_notice_recipients;
        DROP TRIGGER policy_publication_no_truncate ON policy_notice_publications;
        DROP TRIGGER policy_publication_guard ON policy_notice_publications;
        DROP TRIGGER policy_publication_authority ON policy_notice_publications;
        DROP FUNCTION public.swoovr_policy_recipient();
        DROP FUNCTION public.swoovr_policy_publication();
        DROP FUNCTION public.swoovr_policy_authority();
        """;
}
