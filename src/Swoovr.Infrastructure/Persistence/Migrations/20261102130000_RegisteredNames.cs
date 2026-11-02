using Microsoft.EntityFrameworkCore.Migrations;

namespace Swoovr.Infrastructure.Persistence.Migrations;
public partial class RegisteredNames : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name:"registered_first_name",table:"accounts",type:"character varying(100)",maxLength:100,nullable:true);
        m.AddColumn<string>(name:"registered_last_name",table:"accounts",type:"character varying(100)",maxLength:100,nullable:true);
        m.AddColumn<bool>(name:"registered_mononym",table:"accounts",type:"boolean",nullable:false,defaultValue:false);
        m.AddColumn<DateTimeOffset>(name:"registered_names_confirmed_at",table:"accounts",type:"timestamp with time zone",nullable:true);
        m.AddColumn<long>(name:"registered_names_revision",table:"accounts",type:"bigint",nullable:false,defaultValue:0L);
        m.AddColumn<bool>(name:"registered_names_match_id",table:"accounts",type:"boolean",nullable:true);
        m.AddColumn<long>(name:"registered_names_revision",table:"id_verification_attempts",type:"bigint",nullable:false,defaultValue:0L);
    }
    protected override void Down(MigrationBuilder m)
    {
        foreach(var name in new[]{"registered_first_name","registered_last_name","registered_mononym","registered_names_confirmed_at","registered_names_revision","registered_names_match_id"})m.DropColumn(name,"accounts");
        m.DropColumn("registered_names_revision","id_verification_attempts");
    }
}
