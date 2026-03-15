using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelSystem.Web.Migrations
{
    public partial class AddImageUrlToRoutesAndRemoveQRCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Routes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "QRCode",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IsOpen",
                table: "ShopHours");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QRCode",
                table: "Routes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsOpen",
                table: "ShopHours",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Routes");
        }
    }
}
