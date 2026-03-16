using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLockAndHideToRoutesAndZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Zones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Zones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Zones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Routes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Routes");
        }
    }
}
