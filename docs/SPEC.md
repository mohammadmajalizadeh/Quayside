# Functional specification: container shipping agency management system

Status: Phase A deliverable, draft 1 (2026-11-17). This is the **only** input for Phase B.
It describes behaviour in neutral terms. It contains no code, assets or branding from the
studied application. File layouts needed for compatibility are in `EXCEL_FORMATS.md`; the
analysis evidence behind this spec is kept outside the repository. Items marked **OQ-n** are unresolved (see §11).

Working project name: **"Quayside"** (placeholder, see OQ-1).

---

## 1. Purpose and scope

A desktop system for a **shipping-line agency / freight forwarder** operating mainly at Iranian
ports. It manages:

1. Master data: parties, lines, vessels, voyages, ports, depots, containers, tariffs.
2. Commercial pipeline: inquiry → quotation → booking ("job"), with buy/sell rates and management approval.
3. Documentation: house/master bills of lading for sea (containerised and bulk), air, road and rail; import and export.
4. Container lifecycle: release-to-shipper, full return, discharge, gate-out, empty return, depot and terminal moves.
5. Finance: demurrage/detention calculation, local-charge invoices, pre-invoices, credit/debit notes, payments, statements of account, exchange rates, VAT.
6. Interchange: Excel imports for bulk container events; EDIFACT messages to and from lines and ports.
7. Reporting and administration: users, groups, per-screen permissions, audit, soft delete and restore.

## 2. Glossary

| Term | Meaning |
|---|---|
| B/L, HBL, MBL | Bill of lading; house (forwarder-issued) and master (carrier-issued) |
| Job / Booking | A commercial shipment file from inquiry to booking confirmation |
| Voyage | One call of a vessel at a port, with separate import and export voyage numbers |
| Container (box) | Physical equipment, identified by an ISO 6346 number |
| Container cycle | One use of a box: an **import cycle** (arrives full → returned empty) or an **export cycle** (released empty → returned full → loaded) |
| RTS | Release To Shipper: empty box handed to the exporter |
| F.RTN | Full Return: exporter brings the stuffed box back to port/terminal |
| MT.RTN | Empty (MT) Return: importer returns the emptied box to a depot |
| D/O | Delivery Order: release document for import cargo |
| Demurrage / detention | Daily charge for keeping a box beyond its free days |
| Free time | Days without charge, defined by a demurrage tariff |
| SOC / COC | Shipper-owned / carrier-owned container |
| Cargo type | ST standard, DG dangerous goods, OG out-of-gauge / overweight |
| ROE | Rate of exchange used to convert foreign-currency charges |
| SOA | Statement of account |
| TIP | Temporary import permit document per voyage/line |
| Jalali date | Persian Solar Hijri calendar date (official calendar in Iran) |

## 3. Actors and roles

- **Sales / CRM user**: clients, inquiries, quotations, bookings, tasks.
- **Sales manager**: approves quotations and bookings; receives "ball passed" steps.
- **Documentation clerk**: B/Ls, container lists, RTS/F.RTN/MT.RTN, arrival notices, D/O.
- **Operations / terminal clerk**: container gate-in/out, terminal bookings, inventory.
- **Finance clerk**: invoices, demurrage, payments, ROE, SOA, finalisation.
- **Administrator**: users, groups, permissions, lookups, backup, purge and restore.

Users belong to one branch or to all branches (OQ-2). Permissions are granted per screen and action to groups.

## 4. Entities (Phase B model, neutral naming)

Every entity has `Id`, `CreatedAt/By`, `UpdatedAt/By`, and a **soft-delete state** (`IsDeleted`,
`DeletedAt/By`). See BR-G1 to BR-G3.

### 4.1 Reference data
- **Party**: code, name (English), name (Farsi), national ID, economic code, registration no., postal code, addresses (En/Fa), phones, fax, mobile, email, manager name, category, alert note, assigned salesperson (with change date). A party has one or more **PartyRoles**: Client, Shipper, Consignee, Notify, Forwarder, LineAgent, Bank, Carrier, TransitAgent, Depot operator (OQ-3 for the full list).
- **ShippingLine**: code, name, type, agency party, list of permitted container owner prefixes (BR-C2).
- **Vessel**: code, name, EDI code.
- **Port** (name, UN/LOCODE), **Country**, **Depot** (code, name), **Branch**.
- **Voyage**: vessel, port, import voyage no., export voyage no., ETA, arrival, start/complete discharge, start/complete load, sailing, departure, unberth; import and export exchange rates; payment lock; legs (origin/destination list).
- Lookups (code + En/Fa name): ContainerSize, ContainerType (with demurrage rate group), Currency, MovementType (FCL/LCL/…), BodyState, IOType (Import/Export), TransportMode (Sea-container, Sea-bulk, Air, Road, Rail), FreightTerm (Prepaid/Collect), PackageType, ShipmentType, Incoterm, ExitType, ReleaseType, InvoiceType, InvoiceSubject, PayType, VAT rate.

### 4.2 Commercial
- **Job** (booking file): job no., booking no., line booking no., dates (job, quotation, booking), client, booker, forwarder, salesperson, line, POR/POL/POD/final destination, POD free time, shipment type, transport mode, movement type, freight term, commodity, packages, gross weight, dimensions (L/W/H), stackable flag, cargo value + currency, current workflow step, remarks, "ready for commission" flags (sales and finance).
- **JobBoxLine**: quantity × size × type (× special-equipment code).
- **JobRateLine**: service, box line, quantity, buy unit fee/currency/VAT, sell unit fee/currency/VAT, remark.
- **JobStepHistory**: step, passed at, by user.
- **JobParties**: consignee, notify, delivery party, invoice party, cancel/lose reason.
- **AgentRate**: buy rates received from overseas agents per box line.

### 4.3 Documentation
- **BillOfLading**: internal file no., house no., master no., job/booking no., direction (import/export), transport mode, shipment type, line, line agency, voyage and first voyage, POR/POL/POT/POD/final destination (coded plus free text), shipper/consignee/notify (coded plus free text), delivery, invoice, booker, forwarder and agent parties, salesperson, goods description, HS code, packages, package type, gross weight, CBM, chargeable weight, incoterm, freight term (house/master), free-time tariff (house/master), issue/release dates and release type (house/master), D/O send/receive dates and branch, exit type, payment lock, alert remark.
- **BLCargoUnit**: links a B/L to one cargo unit (container cycle, truck or air piece) with packages, gross and net weight, discharge date and remark.
- **BLExtra**: issue city, receive date, allowed depot, co-load B/L, commission, exhibition/ATA-carnet fields (OQ-4).
- **BLLink**: groups related B/Ls in sequence. **BLCorrection**: audit of field corrections.
- **ArrivalNotice** with charge lines; **ShippingNotice**; **TransitFile** with per-container dispatch, border crossing, destination arrival and delivery dates.

### 4.4 Containers
- **Container** (equipment master): number, size, type, tare, max gross, owner line, produced, bought, condition %.
- **ContainerCycle**: container, line, transport mode, booking no., full/empty, FCL/LCL, SOC flag, movement type, seal, DG (class, UN no., packing group, technical name, temperature), reefer temperature, out-of-gauge L/W/H, export permit, cargo type (ST/DG/OG), and lifecycle dates:
  - export: `ReleasedToShipperAt`, `ShipperPartyId`, `FullReturnedAt`
  - import: `GateOutAt`, `FactoryArrivalAt`, `StrippedAt`, `EmptyLeftFactoryAt`, `EmptyReturnedAt`, `EmptyReturnDepotId`, `EmptyReturnBodyStateId`, `EmptyReturnConfirmed`, `DispatchedAt`, `BorderArrivalAt`
- **ContainerMove** (terminal/yard event): in/out, date, truck, driver, tally man, delivery/invoice party, remark, damage records (side, type, level, repair start/finish).

### 4.5 Finance
- **DemurrageTariff**: code, name, kind (import demurrage / export detention, OQ-5), currency, free days, active, owner (line/company), slabs.
- **DemurrageSlab**: sequence, from day, to day (null = open-ended), daily fee for 20′, daily fee for 40′.
- **Service** (charge code): code, En/Fa name, default VAT, D/O-related flag, sort order.
- **LocalTariffRow**: service, line, IO type, movement type, shipment type, full/empty, SOC/COC, size, type, POD, exit type, basis (per box / per B/L / per ton…, OQ-6), fee, currency, valid from/to.
- **Invoice**: number (draft numbers are negative), serial per invoice type when final, B/L, voyage, type, model, subject, buy/sell side, invoice party, payer, delivery party, invoice date, draft date, courier date, status, branch, VAT %, demurrage tariff, print template, linked invoice (buy↔sell), reference, remarks.
- **InvoiceLine**: service, container cycle (optional), size/type, quantity, unit fee, amount, currency, discount, VAT %, VAT amount, period from/to, free days, chargeable days, remark.
- **Payment**: party, pay type, side (received/paid), amount, currency, pay date, actual date, cheque date, SWIFT date, document no., bank account, branch, status.
- **PaymentAllocation**: payment → invoice, amount used.
- **ExchangeRate**: currency, date, rate, kind, optional scope (invoice/B/L/job).

### 4.6 Administration
User (login, name, branch, active, password hash), Group, Membership, ScreenPermission (group × screen × actions), AuditLog, SavedQuery (read-only, OQ-7), PrintTemplate, EmailRecipientCategory, Task (to-do: title, client, owner, priority, category, start, finish, remarks), EdiCodeMapping (external code ↔ internal lookup).

## 5. Workflows

### W1. Commercial (CRM)
1. Sales creates a **job** at the *Inquiry* step with client, route, boxes and cargo.
2. Buy rates (from line/agents) and sell rates are added per box and service. Buy/sell must exist before submitting (BR-J2).
3. Step sequence: Inquiry → Inquiry manager confirmation → Quotation → Quotation manager confirmation → Client confirmation → Booking → Booking manager confirmation. Each pass is recorded with user and time (OQ-8 for the exact steps). Once a step has passed upward, lower-level users cannot edit (BR-J3).
4. A confirmed booking gets a booking number. Line booking no. is recorded.
5. Lost or cancelled jobs record a reason. Commission eligibility is flagged separately by sales and finance.
6. Supporting: client onboarding requests (request → approval → party created), "my clients", tasks/to-do, client activity and job progress reports.

### W2. Export container cycle
1. **RTS**: for a booking, empty boxes are released to the shipper (manual or Excel `RTS.xlsx`). This creates export cycles linked to the booking and shipper.
2. **F.RTN**: the full box returns (manual or `FRTN.xlsx`). Cargo details may arrive at the same time and be attached to the export B/L (`F.RTN + Add to BL.xlsx`).
3. Export B/L created (or copied from an existing B/L or job). Containers are attached and validated (BR-C*).
4. Loading list per voyage. COPARN message to the port. Departure finalising.
5. RTS can be cancelled (records cancel date and applicable tariff).
6. Detention charged for days between RTS and F.RTN beyond free time (BR-D*).

### W3. Import container cycle
1. Voyage created and manifest received (EDI BAPLIE/CUSCAR, custom format, or manual). Unknown codes must be mapped first (BR-E1).
2. Import B/Ls with containers (bulk via `Read CNTRs.xlsx`).
3. Arrival notice issued with charges. Arrival finalising.
4. Discharge date recorded per cargo unit.
5. D/O issued after charges are settled (D/O dashboard, group D/O).
6. Gate-out, optional factory arrival, stripping and empty factory exit.
7. **MT.RTN**: empty return to a depot with body state (manual or `MT RTN.xlsx`), then confirmation.
8. Demurrage invoice from discharge to empty return (BR-D*).

### W4. Other transport modes
Air (flight/AWB), road (truck, CMR, TIR, border crossing) and rail B/Ls, inbound and outbound, plus bulk sea B/Ls. Same B/L header, with different cargo-unit types. Transit files track inland transit to destination.

### W5. Invoicing
1. Create a **draft** (pre-invoice) for a B/L: local charges from tariff, demurrage, freight charges, or manual lines. The draft gets the next negative number.
2. Apply discount and VAT. Set ROE for foreign-currency lines.
3. **Finalise**: assigns the next serial within the invoice type. Further edits are locked once the invoice is sent to finance (BR-I3).
4. Buy invoices (from line/terminal) can be linked to sell invoices for margin.
5. Credit and debit notes adjust final invoices.
6. Export of invoices and payments to Excel, including a tax-office format (OQ-9).

### W6. Payments
Record a receipt/payment, allocate it to one or more invoices, track cheque and SWIFT dates, and produce SOA/balance per party.

### W7. Terminal operations
Terminal inbound/outbound bookings, gate-in/out with truck/driver/tally man, damage recording, inventory, idle-time and stay-time reports, and container move invoicing.

### W8. Administration
User and group management, password change, record locking (OQ-10), log viewer, database backup, purge of deleted records with restore ("revive"), forced client version check (not needed for Phase B).

## 6. Screens (described, not copied)

Main window: menu groups **Define**, **CRM**, **Documentation**, **Operation**, **Finance**, **Report**, **Accessories**, **Help**, filtered by permission. Language toggle En/Fa for printouts.

| Area | Screens |
|---|---|
| Define | Company info, bank accounts, file-number series, lines, ports, vessels, voyages (+legs), flights, road trips, containers, demurrage tariffs, local tariff/services, pay types, ROE, page/print templates, email recipient categories, EDI code mappings, parties (+details, +photos/attachments) |
| CRM | Job/booking editor (box, dimension, other-details and rate sub-dialogs), agent rates, approval queues (inquiry/quotation/booking manager confirmation, client confirmation), approval history, my clients, my tasks, to-do list, new-client requests, merge clients, RTS cancelling, commission, ready-for-commission |
| Documentation | B/L editor for each mode and direction (header tabs, cargo-unit grid, details), B/L linking, B/L group update, B/L correction, RTS, F.RTN, MT.RTN, freight charges, arrival notice, shipping notice, arrival/departure finalising, D/O dashboard, group D/O |
| Operation | Inbound/outbound container moves, terminal bookings (+box details, +container), container movements, damage details |
| Finance | Invoice editor (draft/final, demurrage invoice, discount/VAT, ordered-by), payment editor, buy-sell link, invoice finalising, confirm container-move invoices, financial document export |
| Report | listed in §7 |
| Accessories | Read EDI (standard/custom), write EDI (line/port), read Excel of container movements, saved queries, log viewer, locking, user/group management, change password, backup |

Common UI patterns to keep: search/filter grid on every list, **Export grid to Excel**, copy-to-clipboard shortcuts (B/L no., booking no., national ID, English name, email), "create new from copy of current" for B/Ls and jobs, attachments per record.

## 7. Reports and import/export

### 7.1 Reports (each filterable by date range/line/voyage/party/branch, exportable to Excel/PDF)
Booking report; job progress; client activity; all/sea/air/road shipments; voyage report; line report (head-office report); transit report; container history; container move history; container inventory (agency and terminal); terminal I/O; container idle report; container stay time; TIP report; invoice report; payment report; revenue report; SOA; agents-rate report; port activity; loading/discharging lists (per B/L or whole voyage); B/L cover sheets (sea/air/overland); B/L print (English/Farsi formats per line, OQ-11); arrival notice (En/Fa); D/O letter.

### 7.2 Excel import/export (must be compatible, see `EXCEL_FORMATS.md`)
| Template | Import action | Key |
|---|---|---|
| DefineCNTR | upsert containers by number | container no. |
| Read CNTRs | attach container cycles + cargo to the selected B/L | container no. |
| F.RTN + Add to BL | set full-return date and attach to the B/L given in `H.BL` | container no. + house B/L |
| RTS | create export cycles for booking | container no. + booking no. |
| FRTN | set full-return date on open export cycle | container no. |
| MT RTN | set empty-return date, body state, depot on open import cycle | container no. |

Import rules:
- The header row is matched case-insensitively with trimmed whitespace. Extra empty columns are ignored.
- Container numbers are normalised (upper-case, dash and space removed).
- Dates are accepted as Excel dates or `yyyy/mm/dd`, `yyyy-mm-dd`, `m/d/yyyy` text.
- **Preview first, then commit.** Each row gets a result (OK / warning / error with message), and nothing is saved while errors exist unless the user chooses "import valid rows only".
- Each template can be **exported** with the same header text, column order, column count and value formats as the sample, so import → export → import is lossless (Definition of Done).

### 7.3 EDI (UN/EDIFACT)
- Write **COPARN D:00A** (container announcement) for a voyage/booking to the port or line.
- Read **BAPLIE** (bay plan) and **CUSCAR** (customs cargo report) to populate voyage containers and B/Ls; read a configurable CSV/fixed "custom format".
- An external-to-internal code mapping table resolves line, vessel, port, size/type, package and similar codes.
- Lower priority than Excel. Exact segment usage is in OQ-12.

## 8. Business rules (each needs a unit test)

**General**
- **BR-G1** Deletion is soft. Deleted records are hidden from lists and are read-only until restored. An admin purge moves them to an archive store.
- **BR-G2** A record referenced by another live record cannot be deleted. The error lists the referencing records.
- **BR-G3** Every save stamps user and time. Corrections to issued B/Ls are logged with old and new values.
- **BR-G4** Dates are stored as Gregorian (UTC date). The UI shows and accepts Jalali or Gregorian according to the user setting. Fields documented in both calendars are a single stored value (OQ-13).
- **BR-G5** A user restricted to one branch sees and creates records only in that branch.

**Containers**
- **BR-C1** A container number is 4 letters + 7 digits after normalisation. The ISO 6346 check digit is validated, and a mismatch is a **warning**, not an error (OQ-14).
- **BR-C2** If a line defines permitted owner prefixes, a container attached to that line's B/L must start with one of them.
- **BR-C3** Cargo type `ST` is invalid when the cycle has a DG class or UN number. `DG` is invalid when it has neither.
- **BR-C4** Cargo type `OG` is required when gross weight exceeds the container's max gross or the out-of-gauge dimensions are set (OQ-15 for the exact threshold).
- **BR-C5** A container cannot appear twice on the same B/L or in the same import batch.
- **BR-C6** A container cycle cannot be deleted if the box has a later import or export cycle.
- **BR-C7** Terminal gate-in is rejected if the box is already in terminal inventory. Gate-out is rejected if it is not.
- **BR-C8** A container's size/type/tare are those of the equipment master. Importing a different size/type for an existing number is an error.

**Lifecycle dates**
- **BR-L1** F.RTN cannot be earlier than RTS. MT.RTN cannot be earlier than the discharge date.
- **BR-L2** MT.RTN can be saved only if the cargo unit has a discharge date and the B/L has a D/O date.
- **BR-L3** MT.RTN cannot be overwritten through import/entry if one already exists (explicit "change" action required).
- **BR-L4** F.RTN or MT.RTN cannot be changed or removed once a demurrage/detention invoice covers that container.
- **BR-L5** A return date cannot be deleted if it is empty (no-op error).
- **BR-L6** A B/L's first voyage cannot be changed while it has containers. A B/L's cargo units must be deleted before the B/L is deleted.

**Demurrage / detention**
- **BR-D1** A tariff has free days *F* and ≥ 1 slab. Slab 1 covers days 1..F with no fee. Each next slab starts at previous `to` + 1. Only the last slab has an empty `to`. Fees are ≥ 0.
- **BR-D2** Chargeable period: import = discharge → empty return; export = RTS → full return (OQ-16: inclusive day counting and same-day rules).
- **BR-D3** Charge = Σ over slabs of (days in slab beyond free time × daily fee for the size class). 20′ uses the 20′ fee. 40′ and 45′ use the 40′ fee (OQ-17).
- **BR-D4** If a return date is missing, demurrage can be calculated "to date" for a future-attention estimate, but cannot be invoiced as final (OQ-18).
- **BR-D5** A B/L can have only one saved demurrage invoice per side (buy/sell). Re-calculation requires cancelling the existing invoice.
- **BR-D6** The tariff applied comes from the B/L's free-time field (house for sell side, master for buy side, OQ-19).

**Invoices and payments**
- **BR-I1** Draft invoices get decreasing negative numbers (−1, −2, …). Finalisation assigns `max(serial for invoice type) + 1`.
- **BR-I2** A rate/tariff list cannot contain the same service twice for the same size and type.
- **BR-I3** An invoice sent to finance or finalised is not editable except via credit/debit note.
- **BR-I4** Line amount = qty × unit fee − discount. VAT = amount × VAT % (service default, overridable). Totals are kept per currency. Foreign currency is converted at the ROE of the invoice (rounding per currency's decimals, OQ-20).
- **BR-I5** A payment allocated to any invoice cannot be deleted. Allocations cannot exceed the payment amount or the invoice's open balance.
- **BR-I6** ROE cannot be changed on a locked booking/voyage (payment lock).

**Commercial**
- **BR-J1** A job's details cannot be added before the job header is saved.
- **BR-J2** A job cannot be submitted for approval without at least one buy and one sell rate.
- **BR-J3** After a job passes to a higher approval step, the submitter cannot edit it.

**EDI**
- **BR-E1** An EDI import aborts with a list of unknown external codes until all of them are mapped.
- **BR-E2** A B/L already loaded on a vessel cannot be re-read from EDI until it is unloaded.

**Security**
- **BR-S1** Passwords are stored hashed. Changing a password requires the old password and a matching confirmation.
- **BR-S2** Access to each screen/action is checked against group permissions. Denied access shows a message and is logged.

## 9. Non-functional requirements
- Windows 10/11 x64 desktop; single user or small LAN team. **SQLite** default (one file), designed so a server DB can be added later (OQ-21).
- English UI first. Farsi (RTL) for printouts and as an optional UI language. Jalali calendar support via `System.Globalization.PersianCalendar`.
- Parameterised data access only. No embedded credentials. Settings under the user profile.
- Excel I/O via ClosedXML (.xlsx). Legacy `.xls` is not supported (OQ-22).
- Reports rendered to PDF/print. Engine to be chosen in Phase B (e.g. QuestPDF, community licence, or a pure-MIT alternative; OQ-23).

## 10. Phase B priority (proposed)
1. Reference data + containers master (+ DefineCNTR import/export)
2. B/L with cargo units (+ Read CNTRs, F.RTN + Add to BL)
3. Lifecycle events RTS/F.RTN/MT.RTN (+ the three Excel templates)
4. Demurrage tariffs + calculation + demurrage invoice
5. Invoices, ROE, VAT, payments, SOA
6. Jobs/CRM with approval steps
7. Reports
8. Users/groups/permissions
9. EDI (COPARN out, BAPLIE/CUSCAR in)

Steps 1–4 are enough to meet the Definition of Done (Excel round-trip). Later steps can ship incrementally.

## 11. Open questions
- **OQ-1** Project name/branding for the new app. "Quayside" is a placeholder. Please confirm or replace.
- **OQ-2** Branch model: number of branches, and whether data is shared across them.
- **OQ-3** Full list of party roles (entity types) and their codes.
- **OQ-4** Are exhibition/ATA-carnet and transit-bond features needed?
- **OQ-5** Demurrage tariff "type" values. One rule requires a tariff name to start with "U"; its meaning is unknown.
- **OQ-6** Local tariff "basis" values (per box, per B/L, per ton, per CBM?).
- **OQ-7** The original allows ad-hoc saved SQL queries. Keep as read-only, or drop?
- **OQ-8** Exact approval step names/order, and who may pass each.
- **OQ-9** Tax-office Excel export layout (no sample provided).
- **OQ-10** Meaning of the "Locking" feature (record lock vs period close).
- **OQ-11** B/L print layouts are line-specific in the original. Which lines/formats are needed? New layouts will be designed from scratch.
- **OQ-12** Sample EDI files (BAPLIE, CUSCAR, COPARN) are needed to confirm segment usage. None were provided.
- **OQ-13** Several `...H`/`...M` field pairs look like **H**ijri/**M**iladi dates. Others (release type, freight type, free time) look like **H**ouse/**M**aster. Please confirm per field.
- **OQ-14** Should a wrong ISO 6346 check digit be an error or a warning? Samples include odd-format and lower-case numbers.
- **OQ-15** OG (overweight) threshold: container max gross, a fixed limit, or a line-specific rule?
- **OQ-16** Day counting for demurrage: are both start and end dates counted? Which date starts the clock (discharge vs. vessel arrival)?
- **OQ-17** Is a separate 45′ (or reefer-specific) fee needed? The original only has 20/40 fee columns, plus a per-type "demurrage rate" factor on container types.
- **OQ-18** Is "future attention" (projected demurrage to a chosen date) needed in v1?
- **OQ-19** Which free-time field applies to which invoice side?
- **OQ-20** Currency rounding: IRR (0 decimals) vs USD/EUR (2)? Is a VAT % history needed?
- **OQ-21** Is multi-user access over the network required for v1 (would require SQL Server/PostgreSQL rather than SQLite)?
- **OQ-22** Is legacy `.xls` import needed?
- **OQ-23** Report engine licence preference (QuestPDF community licence vs. strictly MIT/Apache components).
- **OQ-X1** Meaning of the **`Code`** column present in five Excel templates (values such as 114, 3106, 3201, 3202, `32035C`). It is constant within a file and differs across files. It might be a batch/session number, a user code, a voyage/file code or a depot code. **Phase B will import and re-export it verbatim as a batch reference** until clarified.
- **OQ-X2** The sample workbooks appear to contain real operational data. Confirm they may be used locally for round-trip testing. Synthetic fixtures with the same layout will be committed instead.
