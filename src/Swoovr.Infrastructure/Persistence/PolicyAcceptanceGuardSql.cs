namespace Swoovr.Infrastructure.Persistence;
public static class PolicyAcceptanceGuardSql
{
    public const string Up="""
        CREATE TRIGGER policy_acceptance_authority BEFORE INSERT OR UPDATE OR DELETE ON policy_acceptances FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_policy_authority();
        CREATE FUNCTION public.swoovr_policy_acceptance() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
        BEGIN
            IF TG_OP='UPDATE' OR TG_OP='TRUNCATE' THEN RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy acceptance is immutable';END IF;
            IF TG_OP='INSERT' THEN
                IF NEW.accepted_at>CURRENT_TIMESTAMP+interval '5 seconds' OR NOT EXISTS(SELECT 1 FROM accounts a WHERE a.id=NEW.account_id AND a.realm='Members' AND a.status='Active') THEN
                    RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy acceptance requires a current member source';
                END IF;
                RETURN NEW;
            END IF;
            IF NOT EXISTS(SELECT 1 FROM accounts a WHERE a.id=OLD.account_id AND a.status='PendingDeletion' AND a.deletion_finalizing_at IS NOT NULL)
                OR EXISTS(SELECT 1 FROM legal_holds h WHERE h.related_account_ids IS NULL OR h.retention_case_ids IS NULL)
                OR EXISTS(SELECT 1 FROM legal_holds h JOIN legal_hold_scopes sc ON sc.hold_id=h.id
                    WHERE h.status<>'Released' AND sc.kind='Account' AND sc.target_id=OLD.account_id
                    AND NOT EXISTS(SELECT 1 FROM evidence_items e WHERE e.hold_id=h.id AND e.kind='policy-acceptance'
                        AND e.source='policy_acceptances' AND e.record_id=OLD.account_id::text||'/'||OLD.version AND e.account_id=OLD.account_id
                        AND octet_length(e.sha256)=32 AND octet_length(e.sealed)>29)) THEN
                RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Policy acceptance disposal requires final deletion and preservation authority';
            END IF;
            RETURN OLD;
        END $$;
        CREATE TRIGGER policy_acceptance_guard BEFORE INSERT OR UPDATE OR DELETE ON policy_acceptances FOR EACH ROW EXECUTE FUNCTION public.swoovr_policy_acceptance();
        CREATE TRIGGER policy_acceptance_no_truncate BEFORE TRUNCATE ON policy_acceptances FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_policy_acceptance();
        """;
    public const string Down="""
        DROP TRIGGER policy_acceptance_no_truncate ON policy_acceptances;
        DROP TRIGGER policy_acceptance_guard ON policy_acceptances;
        DROP TRIGGER policy_acceptance_authority ON policy_acceptances;
        DROP FUNCTION public.swoovr_policy_acceptance();
        """;
}
