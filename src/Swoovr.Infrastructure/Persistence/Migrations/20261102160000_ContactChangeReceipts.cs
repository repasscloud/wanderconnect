using Microsoft.EntityFrameworkCore.Migrations;

namespace Swoovr.Infrastructure.Persistence.Migrations;

public partial class ContactChangeReceipts : Migration
{
    // Existing queued codes have no proven request session and are never backfilled with invented evidence.
    protected override void Up(MigrationBuilder m)=>m.AddColumn<byte[]>(name:"request_evidence",table:"phone_codes",type:"bytea",nullable:true);
    protected override void Down(MigrationBuilder m)=>m.DropColumn(name:"request_evidence",table:"phone_codes");
}
