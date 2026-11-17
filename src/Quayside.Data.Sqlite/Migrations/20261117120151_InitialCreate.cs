using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quayside.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BodyStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemurrageTariffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    FreeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemurrageTariffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Depots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Depots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    EffectiveOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NameFa = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Roles = table.Column<int>(type: "INTEGER", nullable: false),
                    NationalId = table.Column<string>(type: "TEXT", nullable: true),
                    EconomicCode = table.Column<string>(type: "TEXT", nullable: true),
                    AddressEn = table.Column<string>(type: "TEXT", nullable: true),
                    AddressFa = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vessels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    EdiCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vessels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemurrageSlabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TariffId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromDay = table.Column<int>(type: "INTEGER", nullable: false),
                    ToDay = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemurrageSlabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemurrageSlabs_DemurrageTariffs_TariffId",
                        column: x => x.TariffId,
                        principalTable: "DemurrageTariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerPrefixes = table.Column<string>(type: "TEXT", nullable: true),
                    AgencyPartyId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lines_Parties_AgencyPartyId",
                        column: x => x.AgencyPartyId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Voyages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VesselId = table.Column<int>(type: "INTEGER", nullable: false),
                    PortId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportVoyageNo = table.Column<string>(type: "TEXT", nullable: true),
                    ExportVoyageNo = table.Column<string>(type: "TEXT", nullable: true),
                    Eta = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ArrivedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SailedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PaymentLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Voyages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Voyages_Ports_PortId",
                        column: x => x.PortId,
                        principalTable: "Ports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Voyages_Vessels_VesselId",
                        column: x => x.VesselId,
                        principalTable: "Vessels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemurrageRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SlabId = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentClass = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemurrageRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemurrageRates_DemurrageSlabs_SlabId",
                        column: x => x.SlabId,
                        principalTable: "DemurrageSlabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookingNo = table.Column<string>(type: "TEXT", nullable: false),
                    BookedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    LineId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Lines_LineId",
                        column: x => x.LineId,
                        principalTable: "Lines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Parties_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    SizeFeet = table.Column<int>(type: "INTEGER", nullable: false),
                    TypeCode = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    TareKg = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxGrossKg = table.Column<int>(type: "INTEGER", nullable: true),
                    LineId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Containers_Lines_LineId",
                        column: x => x.LineId,
                        principalTable: "Lines",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BillsOfLading",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseNo = table.Column<string>(type: "TEXT", nullable: false),
                    MasterNo = table.Column<string>(type: "TEXT", nullable: true),
                    FileNo = table.Column<string>(type: "TEXT", nullable: true),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    LineId = table.Column<int>(type: "INTEGER", nullable: true),
                    VoyageId = table.Column<int>(type: "INTEGER", nullable: true),
                    BookingId = table.Column<int>(type: "INTEGER", nullable: true),
                    ShipperId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConsigneeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ShipperText = table.Column<string>(type: "TEXT", nullable: true),
                    ConsigneeText = table.Column<string>(type: "TEXT", nullable: true),
                    NotifyText = table.Column<string>(type: "TEXT", nullable: true),
                    PolId = table.Column<int>(type: "INTEGER", nullable: true),
                    PodId = table.Column<int>(type: "INTEGER", nullable: true),
                    GoodsDescription = table.Column<string>(type: "TEXT", nullable: true),
                    HsCode = table.Column<string>(type: "TEXT", nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DeliveryOrderOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DemurrageTariffId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillsOfLading", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_DemurrageTariffs_DemurrageTariffId",
                        column: x => x.DemurrageTariffId,
                        principalTable: "DemurrageTariffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Lines_LineId",
                        column: x => x.LineId,
                        principalTable: "Lines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Parties_ConsigneeId",
                        column: x => x.ConsigneeId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Parties_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Ports_PodId",
                        column: x => x.PodId,
                        principalTable: "Ports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Ports_PolId",
                        column: x => x.PolId,
                        principalTable: "Ports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillsOfLading_Voyages_VoyageId",
                        column: x => x.VoyageId,
                        principalTable: "Voyages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContainerCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    BillOfLadingId = table.Column<int>(type: "INTEGER", nullable: true),
                    BookingId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsFull = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsShipperOwned = table.Column<bool>(type: "INTEGER", nullable: false),
                    MovementType = table.Column<string>(type: "TEXT", nullable: true),
                    SealNo = table.Column<string>(type: "TEXT", nullable: true),
                    DgClass = table.Column<string>(type: "TEXT", nullable: true),
                    UnNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ReeferTempC = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    Packages = table.Column<int>(type: "INTEGER", nullable: true),
                    GrossWeightKg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    NetWeightKg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    CargoType = table.Column<int>(type: "INTEGER", nullable: false),
                    ExportPermit = table.Column<string>(type: "TEXT", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    ReleasedToShipperOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FullReturnedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DischargedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    GateOutOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EmptyReturnedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EmptyReturnDepotId = table.Column<int>(type: "INTEGER", nullable: true),
                    EmptyReturnBodyStateId = table.Column<int>(type: "INTEGER", nullable: true),
                    EmptyReturnConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReleaseBatchRef = table.Column<string>(type: "TEXT", nullable: true),
                    FullReturnBatchRef = table.Column<string>(type: "TEXT", nullable: true),
                    EmptyReturnBatchRef = table.Column<string>(type: "TEXT", nullable: true),
                    AttachBatchRef = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerCycles_BillsOfLading_BillOfLadingId",
                        column: x => x.BillOfLadingId,
                        principalTable: "BillsOfLading",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContainerCycles_BodyStates_EmptyReturnBodyStateId",
                        column: x => x.EmptyReturnBodyStateId,
                        principalTable: "BodyStates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContainerCycles_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContainerCycles_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContainerCycles_Depots_EmptyReturnDepotId",
                        column: x => x.EmptyReturnDepotId,
                        principalTable: "Depots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DraftNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TypeCode = table.Column<string>(type: "TEXT", nullable: false),
                    Serial = table.Column<int>(type: "INTEGER", nullable: true),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    BillOfLadingId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartyId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    DemurrageTariffId = table.Column<int>(type: "INTEGER", nullable: true),
                    AdjustsInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_BillsOfLading_BillOfLadingId",
                        column: x => x.BillOfLadingId,
                        principalTable: "BillsOfLading",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceCode = table.Column<string>(type: "TEXT", nullable: true),
                    ContainerCycleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Discount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    VatPercent = table.Column<decimal>(type: "TEXT", precision: 7, scale: 4, nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PeriodTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FreeDays = table.Column<int>(type: "INTEGER", nullable: true),
                    ChargeableDays = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_ContainerCycles_ContainerCycleId",
                        column: x => x.ContainerCycleId,
                        principalTable: "ContainerCycles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaymentId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_BookingId",
                table: "BillsOfLading",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_ConsigneeId",
                table: "BillsOfLading",
                column: "ConsigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_DemurrageTariffId",
                table: "BillsOfLading",
                column: "DemurrageTariffId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_HouseNo",
                table: "BillsOfLading",
                column: "HouseNo");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_LineId",
                table: "BillsOfLading",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_PodId",
                table: "BillsOfLading",
                column: "PodId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_PolId",
                table: "BillsOfLading",
                column: "PolId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_ShipperId",
                table: "BillsOfLading",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfLading_VoyageId",
                table: "BillsOfLading",
                column: "VoyageId");

            migrationBuilder.CreateIndex(
                name: "IX_BodyStates_Code",
                table: "BodyStates",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingNo",
                table: "Bookings",
                column: "BookingNo",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClientId",
                table: "Bookings",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_LineId",
                table: "Bookings",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCycles_BillOfLadingId",
                table: "ContainerCycles",
                column: "BillOfLadingId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCycles_BookingId",
                table: "ContainerCycles",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCycles_ContainerId",
                table: "ContainerCycles",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCycles_EmptyReturnBodyStateId",
                table: "ContainerCycles",
                column: "EmptyReturnBodyStateId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCycles_EmptyReturnDepotId",
                table: "ContainerCycles",
                column: "EmptyReturnDepotId");

            migrationBuilder.CreateIndex(
                name: "IX_Containers_LineId",
                table: "Containers",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_Containers_Number",
                table: "Containers",
                column: "Number",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DemurrageRates_SlabId_EquipmentClass",
                table: "DemurrageRates",
                columns: new[] { "SlabId", "EquipmentClass" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemurrageSlabs_TariffId",
                table: "DemurrageSlabs",
                column: "TariffId");

            migrationBuilder.CreateIndex(
                name: "IX_DemurrageTariffs_Code",
                table: "DemurrageTariffs",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Depots_Name",
                table: "Depots",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CurrencyCode_EffectiveOn",
                table: "ExchangeRates",
                columns: new[] { "CurrencyCode", "EffectiveOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_ContainerCycleId",
                table: "InvoiceLines",
                column: "ContainerCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillOfLadingId",
                table: "Invoices",
                column: "BillOfLadingId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DraftNumber",
                table: "Invoices",
                column: "DraftNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PartyId",
                table: "Invoices",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TypeCode_Serial",
                table: "Invoices",
                columns: new[] { "TypeCode", "Serial" },
                unique: true,
                filter: "\"Serial\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Lines_AgencyPartyId",
                table: "Lines",
                column: "AgencyPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lines_Code",
                table: "Lines",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Code",
                table: "Parties",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_InvoiceId",
                table: "PaymentAllocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PartyId",
                table: "Payments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Ports_Code",
                table: "Ports",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Key",
                table: "Settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vessels_Code",
                table: "Vessels",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Voyages_PortId",
                table: "Voyages",
                column: "PortId");

            migrationBuilder.CreateIndex(
                name: "IX_Voyages_VesselId",
                table: "Voyages",
                column: "VesselId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemurrageRates");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "DemurrageSlabs");

            migrationBuilder.DropTable(
                name: "ContainerCycles");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "BodyStates");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "Depots");

            migrationBuilder.DropTable(
                name: "BillsOfLading");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "DemurrageTariffs");

            migrationBuilder.DropTable(
                name: "Voyages");

            migrationBuilder.DropTable(
                name: "Lines");

            migrationBuilder.DropTable(
                name: "Ports");

            migrationBuilder.DropTable(
                name: "Vessels");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
