using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelSystem.Web.Migrations
{
    public partial class AddLockAndHideToRoutesAndZones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns to Routes table
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Routes",
                type: "nvarchar(max)",
                nullable: true);

            // Add columns to Zones table
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Zones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Zones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Zones",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove columns from Routes table
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Routes");

            // Remove columns from Zones table
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Zones");
        }
    }
}
