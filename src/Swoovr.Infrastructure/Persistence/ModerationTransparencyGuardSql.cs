namespace Swoovr.Infrastructure.Persistence;

/// <summary>Include Up after creating the mapped table; Down before dropping it. No session bypass/delete authority is introduced.</summary>
public static class ModerationTransparencyGuardSql
{
    public const string Up="""
        CREATE FUNCTION public.swoovr_transparency_fact_authority() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
        BEGIN
            IF current_setting('transaction_isolation')<>'read committed' THEN
                RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Transparency source insertion requires read committed';
            END IF;
            -- BEFORE STATEMENT authority precedes tuple work; same-session existing exclusive authority is reentrant.
            PERFORM pg_advisory_xact_lock_shared(1347568979,0);
            RETURN NULL;
        END $$;
        CREATE TRIGGER transparency_fact_authority BEFORE INSERT ON public.moderation_transparency_facts
            FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_transparency_fact_authority();
        CREATE FUNCTION public.swoovr_transparency_fact_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Transparency action receipts are immutable';
        END $$;
        CREATE TRIGGER transparency_fact_immutable BEFORE UPDATE ON public.moderation_transparency_facts
            FOR EACH ROW EXECUTE FUNCTION public.swoovr_transparency_fact_immutable();
        CREATE TRIGGER transparency_fact_delete_authority BEFORE DELETE ON public.moderation_transparency_facts
            FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_transparency_fact_authority();
        CREATE FUNCTION public.swoovr_transparency_fact_disposal() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE ending integer;
        BEGIN
            ending:=extract(year from OLD.action_at AT TIME ZONE 'UTC')::integer+CASE WHEN extract(month from OLD.action_at AT TIME ZONE 'UTC')>=7 THEN 1 ELSE 0 END;
            IF OLD.action_at>=CURRENT_TIMESTAMP-interval '36 months'
                OR NOT EXISTS(SELECT 1 FROM public.moderation_transparency_snapshots s WHERE s.ending_year=ending)
                OR EXISTS(SELECT 1 FROM public.legal_holds h WHERE
                    (h.retention_case_ids IS NULL OR h.related_account_ids IS NULL OR (h.status<>'Released' AND (OLD.subject_account_id=ANY(h.related_account_ids)
                        OR EXISTS(SELECT 1 FROM public.legal_hold_scopes sc WHERE sc.hold_id=h.id AND sc.kind='Account' AND sc.target_id=OLD.subject_account_id))))
                    OR (OLD.source_case_id IS NOT NULL AND (h.case_id=OLD.source_case_id OR OLD.source_case_id=ANY(h.retention_case_ids)))
                    OR (OLD.source_kind='media-decision' AND h.source_media_id=OLD.source_id))
                OR EXISTS(SELECT 1 FROM public.evidence_items e WHERE e.kind='moderation-transparency' AND e.record_id=OLD.id::text) THEN
                RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Transparency receipt disposal authority unavailable';
            END IF;
            RETURN OLD;
        END $$;
        CREATE TRIGGER transparency_fact_disposal BEFORE DELETE ON public.moderation_transparency_facts
            FOR EACH ROW EXECUTE FUNCTION public.swoovr_transparency_fact_disposal();
        CREATE TRIGGER transparency_fact_no_truncate BEFORE TRUNCATE ON public.moderation_transparency_facts
            FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_transparency_fact_immutable();
        CREATE TRIGGER transparency_snapshot_immutable BEFORE UPDATE OR DELETE ON public.moderation_transparency_snapshots
            FOR EACH ROW EXECUTE FUNCTION public.swoovr_transparency_fact_immutable();
        CREATE TRIGGER transparency_snapshot_no_truncate BEFORE TRUNCATE ON public.moderation_transparency_snapshots
            FOR EACH STATEMENT EXECUTE FUNCTION public.swoovr_transparency_fact_immutable();
        CREATE FUNCTION public.swoovr_transparency_fact_source() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE valid boolean;
        BEGIN
            IF NEW.source_kind='sanction' THEN
                SELECT EXISTS(SELECT 1 FROM public.sanctions s WHERE s.id=NEW.source_id AND s.kind='Termination'
                    AND s.account_id=NEW.subject_account_id AND s.case_id IS NOT DISTINCT FROM NEW.source_case_id
                    AND s.starts_at=NEW.action_at) INTO valid;
            ELSIF NEW.source_kind='content-removal' THEN
                SELECT EXISTS(SELECT 1 FROM public.content_removal r WHERE r.id=NEW.source_id
                    AND r.subject_account_id=NEW.subject_account_id AND r.case_id IS NOT DISTINCT FROM NEW.source_case_id
                    AND r.removed_at=NEW.action_at) INTO valid;
            ELSIF NEW.source_kind='media-decision' THEN
                SELECT EXISTS(SELECT 1 FROM public.media_items m WHERE m.id=NEW.source_id AND m.account_id=NEW.subject_account_id
                    AND m.case_id IS NOT DISTINCT FROM NEW.source_case_id AND m.status IN ('Blocked','Rejected')
                    AND ((m.reviewed_by IS NOT NULL AND m.reviewed_at=NEW.action_at AND NEW.enforcement_mechanism='human')
                        OR (m.reviewed_by IS NULL AND m.screened_at=NEW.action_at AND NEW.enforcement_mechanism='automated-screening'))) INTO valid;
            ELSE
                valid:=false;
            END IF;
            IF NOT valid OR NOT EXISTS(SELECT 1 FROM public.accounts a WHERE a.id=NEW.subject_account_id AND a.realm='Members')
                OR (NEW.source_case_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM public.moderation_cases c WHERE c.id=NEW.source_case_id AND c.subject_account_id=NEW.subject_account_id)) THEN
                RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Transparency action source provenance is invalid';
            END IF;
            RETURN NEW;
        END $$;
        CREATE CONSTRAINT TRIGGER transparency_fact_source AFTER INSERT ON public.moderation_transparency_facts
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.swoovr_transparency_fact_source();
        """;
    public const string Down="""
        DROP TRIGGER IF EXISTS transparency_snapshot_immutable ON public.moderation_transparency_snapshots;
        DROP TRIGGER IF EXISTS transparency_snapshot_no_truncate ON public.moderation_transparency_snapshots;
        DROP TRIGGER IF EXISTS transparency_fact_no_truncate ON public.moderation_transparency_facts;
        DROP TRIGGER IF EXISTS transparency_fact_disposal ON public.moderation_transparency_facts;
        DROP TRIGGER IF EXISTS transparency_fact_delete_authority ON public.moderation_transparency_facts;
        DROP FUNCTION IF EXISTS public.swoovr_transparency_fact_disposal();
        DROP TRIGGER IF EXISTS transparency_fact_authority ON public.moderation_transparency_facts;
        DROP FUNCTION IF EXISTS public.swoovr_transparency_fact_authority();
        DROP TRIGGER IF EXISTS transparency_fact_source ON public.moderation_transparency_facts;
        DROP TRIGGER IF EXISTS transparency_fact_immutable ON public.moderation_transparency_facts;
        DROP FUNCTION IF EXISTS public.swoovr_transparency_fact_source();
        DROP FUNCTION IF EXISTS public.swoovr_transparency_fact_immutable();
        """;
}
