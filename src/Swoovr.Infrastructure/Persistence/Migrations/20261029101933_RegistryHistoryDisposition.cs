using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistryHistoryDisposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ban_dispositions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    effective_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    history_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_dispositions", x => x.id);
                    table.CheckConstraint("ck_ban_disposition_clock", "history_until >= ((effective_end AT TIME ZONE 'UTC') + interval '24 months') AT TIME ZONE 'UTC' AND disposed_at >= history_until");
                    table.CheckConstraint("ck_ban_disposition_kind", "identifier_type IN ('Email','Mobile') AND status IN ('Revoked','Expired')");
                    table.ForeignKey(
                        name: "fk_ban_dispositions_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ban_disposition_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_disposition_reviews", x => x.id);
                    table.CheckConstraint("ck_ban_disposition_review_reason", "length(btrim(reason)) BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "fk_ban_disposition_reviews_ban_dispositions_disposition_id",
                        column: x => x.disposition_id,
                        principalTable: "ban_dispositions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ban_disposition_reviews_case_id_at",
                table: "ban_disposition_reviews",
                columns: new[] { "case_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_ban_disposition_reviews_disposition_id",
                table: "ban_disposition_reviews",
                column: "disposition_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_dispositions_case_id_identifier_type",
                table: "ban_dispositions",
                columns: new[] { "case_id", "identifier_type" });
            migrationBuilder.Sql("""
                -- Statement authority precedes tuple locks. Row guards alone invert application global→row order.
                CREATE FUNCTION ban_disposal_authority() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_TABLE_NAME='ban_dispositions' OR TG_OP='DELETE' THEN
                    -- A retained transaction snapshot cannot observe a hold committed while authority was awaited.
                    IF current_setting('transaction_isolation')<>'read committed' THEN
                      RAISE EXCEPTION 'ban disposal requires read committed isolation';
                    END IF;
                    PERFORM pg_advisory_xact_lock(1347568979,0);
                  ELSE
                    PERFORM pg_advisory_xact_lock_shared(1347568979,0);
                  END IF;
                  RETURN NULL;
                END $$;
                CREATE TRIGGER ban_disposal_authority BEFORE INSERT OR UPDATE OR DELETE ON banned_identifiers FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE TRIGGER ban_disposal_authority BEFORE INSERT OR UPDATE OR DELETE ON ban_transitions FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE TRIGGER ban_disposal_authority BEFORE INSERT OR UPDATE OR DELETE ON ban_case_notes FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE TRIGGER ban_disposal_authority BEFORE INSERT OR UPDATE OR DELETE ON ban_dispositions FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE TRIGGER ban_disposal_authority BEFORE INSERT OR UPDATE OR DELETE ON ban_disposition_reviews FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE FUNCTION ban_case_disposal_safe(source uuid) RETURNS boolean LANGUAGE sql VOLATILE AS $$
                  SELECT EXISTS (
                    SELECT 1 FROM moderation_cases c WHERE c.id=source AND c.status='Closed'
                    AND c.closed_at IS NOT NULL AND c.evidence_purged_at IS NOT NULL
                    AND NOT EXISTS(SELECT 1 FROM appeals a JOIN sanctions s ON s.id=a.sanction_id WHERE s.case_id=c.id AND a.status='Pending')
                    AND ((GREATEST(c.closed_at,c.escalated_at,
                      (SELECT max(t.at) FROM ban_transitions t WHERE t.case_id=c.id AND t.action='account-reinstated'),
                      (SELECT max(r.at) FROM ban_disposition_reviews r WHERE r.case_id=c.id),
                      (SELECT max(a.decided_at) FROM appeals a JOIN sanctions s ON s.id=a.sanction_id WHERE s.case_id=c.id),
                      (SELECT max(e.created_at) FROM evidence_exports e JOIN legal_holds h ON h.id=e.hold_id WHERE h.case_id=c.id OR c.id=ANY(h.retention_case_ids)),
                      (SELECT max(d.disclosed_at) FROM disclosures d JOIN legal_holds h ON h.id=d.hold_id WHERE h.case_id=c.id OR c.id=ANY(h.retention_case_ids))
                    ) AT TIME ZONE 'UTC') + interval '24 months') AT TIME ZONE 'UTC' <= CURRENT_TIMESTAMP
                    AND NOT EXISTS(
                      SELECT 1 FROM legal_holds h WHERE (h.status<>'Released' OR EXISTS(SELECT 1 FROM evidence_items e WHERE e.hold_id=h.id))
                      AND (h.related_account_ids IS NULL OR h.retention_case_ids IS NULL OR h.case_id=c.id OR c.id=ANY(h.retention_case_ids)
                        OR EXISTS(SELECT 1 FROM (
                          SELECT c.subject_account_id AS account_id UNION SELECT r.reporter_account_id FROM reports r WHERE r.case_id=c.id
                        ) people WHERE people.account_id=ANY(h.related_account_ids)
                          OR EXISTS(SELECT 1 FROM legal_hold_scopes scope WHERE scope.hold_id=h.id AND scope.kind='Account' AND scope.target_id=people.account_id)
                          OR EXISTS(SELECT 1 FROM evidence_items e WHERE e.hold_id=h.id AND e.account_id=people.account_id)))
                    )
                  );
                $$;
                CREATE FUNCTION ban_history_disposal_safe(source uuid) RETURNS boolean LANGUAGE sql VOLATILE AS $$
                  SELECT EXISTS(SELECT 1 FROM banned_identifiers b WHERE b.id=source AND b.status IN ('Revoked','Expired')
                    AND CASE WHEN b.status='Revoked' THEN b.revoked_at ELSE b.ban_expires_at END IS NOT NULL
                    AND ((GREATEST(b.banned_at,b.last_reviewed_at,CASE WHEN b.status='Revoked' THEN b.revoked_at ELSE b.ban_expires_at END,
                        (SELECT max(t.at) FROM ban_transitions t WHERE t.banned_identifier_id=b.id AND t.action<>'expired'),
                        (SELECT max(n.at) FROM ban_case_notes n JOIN ban_transitions t ON t.id=n.transition_id WHERE t.banned_identifier_id=b.id AND t.action<>'expired'))
                        AT TIME ZONE 'UTC')+interval '24 months') AT TIME ZONE 'UTC' <= CURRENT_TIMESTAMP
                    AND ban_case_disposal_safe(b.case_id)
                  );
                $$;
                CREATE FUNCTION ban_disposition_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE source banned_identifiers%ROWTYPE;
                BEGIN
                  IF TG_OP='INSERT' THEN
                    SELECT * INTO source FROM banned_identifiers WHERE id=NEW.id;
                    IF NOT FOUND OR NOT ban_history_disposal_safe(NEW.id)
                      OR NOT EXISTS(SELECT 1 FROM ban_transitions t WHERE t.banned_identifier_id=NEW.id AND t.action='created' AND t.old_status IS NULL AND t.new_status='Active')
                      OR EXISTS(SELECT 1 FROM ban_transitions t WHERE t.banned_identifier_id=NEW.id AND
                        (t.case_id<>NEW.case_id OR NOT EXISTS(SELECT 1 FROM ban_case_notes n WHERE n.transition_id=t.id AND n.case_id=NEW.case_id)))
                      OR NOT EXISTS(SELECT 1 FROM ban_transitions t WHERE t.banned_identifier_id=NEW.id AND t.case_id=NEW.case_id
                        AND t.old_status='Active' AND t.new_status=NEW.status AND t.new_expires_at=NEW.effective_end
                        AND ((NEW.status='Revoked' AND t.action='revoked' AND t.actor_account_id=source.revoked_by AND t.at=source.revoked_at)
                          OR (NEW.status='Expired' AND t.action='expired' AND t.actor_account_id IS NULL)))
                      OR NEW.case_id<>source.case_id
                      OR NEW.identifier_type<>source.identifier_type OR NEW.status<>source.status
                      OR NEW.effective_end IS DISTINCT FROM (CASE WHEN source.status='Revoked' THEN source.revoked_at ELSE source.ban_expires_at END)
                      OR NEW.history_until IS DISTINCT FROM ((GREATEST(source.banned_at,source.last_reviewed_at,NEW.effective_end,
                        (SELECT max(t.at) FROM ban_transitions t WHERE t.banned_identifier_id=NEW.id AND t.action<>'expired'),
                        (SELECT max(n.at) FROM ban_case_notes n JOIN ban_transitions t ON t.id=n.transition_id WHERE t.banned_identifier_id=NEW.id AND t.action<>'expired')) AT TIME ZONE 'UTC')+interval '24 months') AT TIME ZONE 'UTC'
                      OR NEW.disposed_at>CURRENT_TIMESTAMP THEN RAISE EXCEPTION 'ban disposition is not eligible'; END IF;
                    RETURN NEW;
                  ELSIF TG_OP='DELETE' THEN
                    IF EXISTS(SELECT 1 FROM banned_identifiers WHERE id=OLD.id)
                      OR NOT ban_case_disposal_safe(OLD.case_id)
                      OR EXISTS(SELECT 1 FROM sanctions WHERE case_id=OLD.case_id AND kind='Termination' AND lifted_at IS NULL)
                      THEN RAISE EXCEPTION 'ban disposition remains required'; END IF;
                    RETURN OLD;
                  END IF;
                  RAISE EXCEPTION 'ban disposition is append-only';
                END $$;
                CREATE TRIGGER ban_dispositions_guard BEFORE INSERT OR UPDATE OR DELETE ON ban_dispositions FOR EACH ROW EXECUTE FUNCTION ban_disposition_guard();
                CREATE TRIGGER ban_dispositions_no_truncate BEFORE TRUNCATE ON ban_dispositions FOR EACH STATEMENT EXECUTE FUNCTION ban_history_append_only();
                CREATE FUNCTION ban_history_bounded_delete() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE source uuid;
                BEGIN
                  IF TG_OP<>'DELETE' THEN RAISE EXCEPTION 'ban history is append-only'; END IF;
                  IF TG_TABLE_NAME='ban_transitions' THEN source=OLD.banned_identifier_id;
                  ELSIF TG_TABLE_NAME='ban_case_notes' THEN SELECT banned_identifier_id INTO source FROM ban_transitions WHERE id=OLD.transition_id;
                  ELSE source=OLD.id; END IF;
                  IF NOT ban_history_disposal_safe(source) OR NOT EXISTS(SELECT 1 FROM ban_dispositions d JOIN banned_identifiers b ON b.id=d.id
                    WHERE d.id=source AND d.case_id=b.case_id AND d.identifier_type=b.identifier_type AND d.status=b.status)
                    THEN RAISE EXCEPTION 'ban history is append-only outside bounded disposal'; END IF;
                  RETURN OLD;
                END $$;
                DROP TRIGGER ban_transitions_no_change ON ban_transitions;
                DROP TRIGGER ban_case_notes_no_change ON ban_case_notes;
                CREATE TRIGGER ban_transitions_no_change BEFORE UPDATE OR DELETE ON ban_transitions FOR EACH ROW EXECUTE FUNCTION ban_history_bounded_delete();
                CREATE TRIGGER ban_case_notes_no_change BEFORE UPDATE OR DELETE ON ban_case_notes FOR EACH ROW EXECUTE FUNCTION ban_history_bounded_delete();
                CREATE TRIGGER banned_identifiers_bounded_delete BEFORE DELETE ON banned_identifiers FOR EACH ROW EXECUTE FUNCTION ban_history_bounded_delete();

                CREATE FUNCTION ban_disposition_review_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE source ban_dispositions%ROWTYPE;
                BEGIN
                  IF TG_OP='INSERT' THEN
                    SELECT * INTO source FROM ban_dispositions WHERE id=NEW.disposition_id;
                    IF NOT FOUND OR NEW.case_id<>source.case_id OR NEW.at<source.disposed_at OR NEW.at>CURRENT_TIMESTAMP
                      OR NEW.actor_account_id='00000000-0000-0000-0000-000000000000'::uuid
                      OR EXISTS(SELECT 1 FROM banned_identifiers WHERE id=source.id)
                      THEN RAISE EXCEPTION 'ban disposition review provenance invalid'; END IF;
                    RETURN NEW;
                  ELSIF TG_OP='DELETE' THEN
                    IF NOT ban_case_disposal_safe(OLD.case_id)
                      OR ((OLD.at AT TIME ZONE 'UTC')+interval '24 months') AT TIME ZONE 'UTC'>CURRENT_TIMESTAMP
                      OR EXISTS(SELECT 1 FROM sanctions WHERE case_id=OLD.case_id AND kind='Termination' AND lifted_at IS NULL)
                      THEN RAISE EXCEPTION 'ban disposition review remains required'; END IF;
                    RETURN OLD;
                  END IF;
                  RAISE EXCEPTION 'ban disposition review is append-only';
                END $$;
                CREATE TRIGGER ban_disposition_reviews_guard BEFORE INSERT OR UPDATE OR DELETE ON ban_disposition_reviews FOR EACH ROW EXECUTE FUNCTION ban_disposition_review_guard();
                CREATE TRIGGER ban_disposition_reviews_no_truncate BEFORE TRUNCATE ON ban_disposition_reviews FOR EACH STATEMENT EXECUTE FUNCTION ban_history_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER ban_disposal_authority ON banned_identifiers;
                DROP TRIGGER ban_disposal_authority ON ban_transitions;
                DROP TRIGGER ban_disposal_authority ON ban_case_notes;
                DROP TABLE ban_disposition_reviews;
                DROP FUNCTION ban_disposition_review_guard();
                DROP TRIGGER banned_identifiers_bounded_delete ON banned_identifiers;
                DROP TRIGGER ban_transitions_no_change ON ban_transitions;
                DROP TRIGGER ban_case_notes_no_change ON ban_case_notes;
                CREATE TRIGGER ban_transitions_no_change BEFORE UPDATE OR DELETE ON ban_transitions FOR EACH ROW EXECUTE FUNCTION ban_history_append_only();
                CREATE TRIGGER ban_case_notes_no_change BEFORE UPDATE OR DELETE ON ban_case_notes FOR EACH ROW EXECUTE FUNCTION ban_history_append_only();
                DROP FUNCTION ban_history_bounded_delete();
                DROP TABLE ban_dispositions;
                DROP FUNCTION ban_disposition_guard();
                DROP FUNCTION ban_history_disposal_safe(uuid);
                DROP FUNCTION ban_case_disposal_safe(uuid);
                DROP FUNCTION ban_disposal_authority();
                """);
        }
    }
}
