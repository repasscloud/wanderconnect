package app.swoovr.identity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;

/** Minimal provider binding policy: no old contact, fingerprint, credential or copied case reason. */
@Entity
@Table(name="swoovr_email_retirement")
public class RetiredContact {
    @Id @Column(name="user_id",length=36,nullable=false) public String userId;
    @Column(name="realm_id",length=36,nullable=false) public String realmId;
    @Column(name="operation_id",length=36,nullable=false,unique=true) public String operationId;
    @Column(name="sink_email",length=254,nullable=false) public String sinkEmail;
    @Column(name="login_retired",nullable=false) public boolean loginRetired;
    @Column(name="retired_at",nullable=false) public Instant retiredAt;
}
