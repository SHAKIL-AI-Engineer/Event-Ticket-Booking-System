using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EventHub.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "eventhub");

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Target = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Detail = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Idempotency",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Scope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Response = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Idempotency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Hash = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Role = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    City = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Address = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Rows = table.Column<int>(type: "integer", nullable: false),
                    SeatsPerRow = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                    table.CheckConstraint("CK_Venue_Capacity", "\"Rows\" BETWEEN 1 AND 26 AND \"SeatsPerRow\" BETWEEN 1 AND 40");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Read = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Category = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Artist = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Image = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    VenueId = table.Column<int>(type: "integer", nullable: false),
                    OrganizerId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    QueueEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinPrice = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Policies = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueId",
                        column: x => x.VenueId,
                        principalSchema: "eventhub",
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                schema: "eventhub",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => new { x.UserId, x.EventId });
                    table.ForeignKey(
                        name: "FK_Favorites_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "eventhub",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Favorites_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Holds",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Subtotal = table.Column<int>(type: "integer", nullable: false),
                    Fee = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holds", x => x.Id);
                    table.CheckConstraint("CK_Hold_Quote", "\"Subtotal\" > 0 AND \"Fee\" >= 0 AND \"Total\" = \"Subtotal\" + \"Fee\"");
                    table.ForeignKey(
                        name: "FK_Holds_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "eventhub",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Holds_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Queue",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdmittedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Queue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Queue_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "eventhub",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Queue_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HoldId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Subtotal = table.Column<int>(type: "integer", nullable: false),
                    Fee = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefundReason = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Order_Quote", "\"Subtotal\" > 0 AND \"Fee\" >= 0 AND \"Total\" = \"Subtotal\" + \"Fee\"");
                    table.ForeignKey(
                        name: "FK_Orders_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "eventhub",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Holds_HoldId",
                        column: x => x.HoldId,
                        principalSchema: "eventhub",
                        principalTable: "Holds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                schema: "eventhub",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Row = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    HoldId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => new { x.EventId, x.Id });
                    table.CheckConstraint("CK_Seat_Price", "\"Price\" > 0");
                    table.CheckConstraint("CK_Seat_State", "(\"Status\" = 'AVAILABLE' AND \"HoldId\" IS NULL AND \"ExpiresAt\" IS NULL) OR (\"Status\" = 'HELD' AND \"HoldId\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL) OR (\"Status\" IN ('ALLOCATED', 'SOLD') AND \"HoldId\" IS NOT NULL AND \"ExpiresAt\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Seats_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "eventhub",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seats_Holds_HoldId",
                        column: x => x.HoldId,
                        principalSchema: "eventhub",
                        principalTable: "Holds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reference = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClientSecret = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Error = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "eventhub",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "eventhub",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoldSeats",
                schema: "eventhub",
                columns: table => new
                {
                    HoldId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SeatId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoldSeats", x => new { x.HoldId, x.SeatId });
                    table.ForeignKey(
                        name: "FK_HoldSeats_Holds_HoldId",
                        column: x => x.HoldId,
                        principalSchema: "eventhub",
                        principalTable: "Holds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldSeats_Seats_EventId_SeatId",
                        columns: x => new { x.EventId, x.SeatId },
                        principalSchema: "eventhub",
                        principalTable: "Seats",
                        principalColumns: new[] { "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    SeatId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    TransferCount = table.Column<int>(type: "integer", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Gate = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("CK_Ticket_State", "(\"IsActive\" = TRUE AND \"Status\" IN ('VALID', 'CHECKED_IN')) OR (\"IsActive\" = FALSE AND \"Status\" = 'REFUNDED')");
                    table.ForeignKey(
                        name: "FK_Tickets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "eventhub",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Seats_EventId_SeatId",
                        columns: x => new { x.EventId, x.SeatId },
                        principalSchema: "eventhub",
                        principalTable: "Seats",
                        principalColumns: new[] { "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scans",
                schema: "eventhub",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StaffId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Gate = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scans_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "eventhub",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Scans_Users_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "eventhub",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganizerId",
                schema: "eventhub",
                table: "Events",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Slug",
                schema: "eventhub",
                table: "Events",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId",
                schema: "eventhub",
                table: "Events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_EventId",
                schema: "eventhub",
                table: "Favorites",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Holds_EventId",
                schema: "eventhub",
                table: "Holds",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Holds_Status_ExpiresAt",
                schema: "eventhub",
                table: "Holds",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Holds_UserId",
                schema: "eventhub",
                table: "Holds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldSeats_EventId_SeatId",
                schema: "eventhub",
                table: "HoldSeats",
                columns: new[] { "EventId", "SeatId" });

            migrationBuilder.CreateIndex(
                name: "IX_Idempotency_UserId_Scope_Key",
                schema: "eventhub",
                table: "Idempotency",
                columns: new[] { "UserId", "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "eventhub",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EventId",
                schema: "eventhub",
                table: "Orders",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_HoldId",
                schema: "eventhub",
                table: "Orders",
                column: "HoldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                schema: "eventhub",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_PublishedAt_Id",
                schema: "eventhub",
                table: "Outbox",
                columns: new[] { "PublishedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                schema: "eventhub",
                table: "Payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Queue_EventId_UserId",
                schema: "eventhub",
                table: "Queue",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Queue_UserId",
                schema: "eventhub",
                table: "Queue",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrderId",
                schema: "eventhub",
                table: "Refunds",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scans_StaffId",
                schema: "eventhub",
                table: "Scans",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_TicketId",
                schema: "eventhub",
                table: "Scans",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seats_HoldId",
                schema: "eventhub",
                table: "Seats",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Status_ExpiresAt",
                schema: "eventhub",
                table: "Seats",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                schema: "eventhub",
                table: "Sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_EventId_SeatId",
                schema: "eventhub",
                table: "Tickets",
                columns: new[] { "EventId", "SeatId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrderId",
                schema: "eventhub",
                table: "Tickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UserId",
                schema: "eventhub",
                table: "Tickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "eventhub",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Audit",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Favorites",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "HoldSeats",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Idempotency",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Queue",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Scans",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Seats",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Holds",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "eventhub");

            migrationBuilder.DropTable(
                name: "Venues",
                schema: "eventhub");
        }
    }
}
