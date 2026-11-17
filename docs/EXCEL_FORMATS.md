# Excel workbook formats

All six workbooks are **flat import templates**: one sheet (`Sheet1`), header in row 1, one record
per row, **no formulas**, no defined names, no data validation, no tables. `MT RTN.xlsx` and
`Read CNTRs.xlsx` carry an autofilter on one column; this is cosmetic. Workbooks were produced
by Excel 2016 or LibreOffice 5. The original app reads them through Excel COM automation
(`Workbooks.Open`, then cell-by-cell) and offers **"Export to Excel"** of grids, with
`*.XLS` as the save filter.

Header strings below are interoperability facts (file layout), not creative content. Phase B must
accept them **exactly**, with case and trailing spaces tolerated (see `CNTR ` in MT RTN).

Common value formats seen:
- **Container number**: ISO 6346 owner+category letters and 7 digits. Both `ABCU1234567` and `ABCU123456-7` (dash before the check digit) occur, and both must be accepted. Lowercase also occurs (`bsiu9227651`).
- **Dates**: true Excel dates (`m/d/yyyy`, `mm-dd-yy`) **or** text `yyyy/mm/dd`. All are Gregorian.
- **Flags**: `Full` 1/0, `SOC` 1/0.

---

## 1. `DefineCNTR.xlsx`: container master import  *(app: Define CNTR → Read from Excel)*

| Col | Header | Type | Meaning |
|---|---|---|---|
| A | Code | int | row/reference code (meaning unclear, see OQ-X1) |
| B | CNTR | text | container number |
| C | Size | int | 20 / 40 / 45 |
| D | Type | text | ISO type group: DC, HC, RF, OT, FR, TK … |
| E | TW | int | tare weight, kg |
| F | Line | text | owning line short code (e.g. `FWD`) |

Business process: register physical containers (equipment master) in bulk.

## 2. `Read CNTRs.xlsx`: containers for a B/L / booking  *(app: BL Details → "R.F.XLS")*

Sheet1 holds the data; Sheet2 and Sheet3 are empty Excel defaults.

| Col | Header | Type | Meaning |
|---|---|---|---|
| A | CNTR | text | container number |
| B | Size | int | 20/40/45 |
| C | Type | text | type group |
| D | TW | int | tare kg |
| E | Full | 0/1 | full (1) or empty (0) |
| F | Seal | text | seal number (`-` = none) |
| G | SOC | 0/1 | shipper-owned container |
| H | MovementType | text | FCL / LCL / … |
| I | Class | text | IMO DG class (blank if none) |
| J | DGCOde | text | UN number (blank if none) |
| K | RfTemp | number | reefer set temperature °C |
| L | NoOfPkg | int | number of packages |
| M | GW | number | gross weight kg |
| N | Nw | number | net weight kg |
| O | CargoType | text | `ST` (standard), `DG` (dangerous), `OG` (out-of-gauge/overweight) |
| P | ExpPer | text | export permit no. |
| Q | Remark | text | free text |

Business process: attach a container list (with cargo details) to a bill of lading in one step.

## 3. `F.RTN + Add to BL.xlsx`: full return + attach to B/L  *(export)*

Same data as #2 plus the return date, with different header spellings:

| Col | Header | Type | Meaning |
|---|---|---|---|
| A | Code | text (e.g. `32035C`) | see OQ-X1 |
| B | H.BL | int/text | house B/L number |
| C | Container No. | text | |
| D | F.RTN Date | text `yyyy/mm/dd` | date full container returned to port/terminal |
| E | Full | 0/1 | |
| F | Seal No | text | |
| G | SOC | 0/1 | |
| H | Movement Type | text | |
| I | Class | text | |
| J | DG Code | text | |
| K | RFTemp | number | |
| L | Pkg | int | |
| M | G.W | number | |
| N | N.W | number | |
| O | ST/DG/OG | text | cargo type |
| P | Exp Per | text | |

Business process: in one step, record that export boxes came back full and link them to their house B/L.

## 4. `RTS.xlsx`: release to shipper  *(app: Documentation → R.T.S)*

| Col | Header | Type | Meaning |
|---|---|---|---|
| A | Code | int | see OQ-X1 |
| B | Container No. | text | |
| C | Date | date | date the empty box was released to the shipper |
| D | BookingNo | int | booking the box is released against |

Columns E–J are empty but formatted. `max_column` is 10, and a round-trip must not fail on them.
Business process: start of the export cycle (an empty box goes out to the shipper for stuffing). This starts export detention.

## 5. `FRTN.xlsx`: full return  *(app: Documentation → F.RTN)*

| Col | Header | Type | Meaning |
|---|---|---|---|
| A | Code | int | see OQ-X1 |
| B | CNTR | text | |
| C | FRTN | date | date the box came back full |

The containers in this sample are the same ones as in RTS.xlsx, returned 4–7 days after release. This stops export detention.

## 6. `MT RTN.xlsx`: empty return  *(app: Documentation → MT.RTN)*

| Col | Header | Type | Meaning |
|---|---|---|---|
| A | Code | int | see OQ-X1 |
| B | `CNTR ` (trailing space) | text | |
| C | MT | date | date the empty box was returned to the depot |
| D | Body | int | body state code (1, 3 seen; lookup "Body State": sound/damaged …) |
| E | Depot | text | depot **name** (e.g. a port city), resolved to a depot by name |

Business process: end of the import cycle. This stops import demurrage. Allowed only when the
container has a discharge date and the B/L has a D/O date, and not if a return date already exists.

---

## Import vs export summary

| Workbook | App imports? | App exports same layout? |
|---|---|---|
| DefineCNTR | yes | grid export (layout not confirmed) |
| Read CNTRs | yes | unknown |
| F.RTN + Add to BL | yes | unknown |
| RTS / FRTN / MT RTN | yes | unknown. "Export to Excel" dumps the visible grid. |

The original exports are generic grid dumps. For Phase B, "export them back without losing data"
is defined as: **exporting a template writes the same headers, column order and values**, so that
re-importing yields identical records (see SPEC §7).

---

## Implementation notes (Phase B)

- Import and export share one column table (`src/Quayside.Excel/Templates.cs`). Exports write the exact
  header text (including the trailing space in `CNTR `), column order, sheet names (`Sheet1`, plus empty
  `Sheet2`/`Sheet3` for the B/L container list), and per-column formats: real dates as `m/d/yyyy` or
  `mm-dd-yy`, and text dates as `yyyy/MM/dd` for the full-return-attach template. Numeric codes stay numeric.
- "Without losing data" is checked **semantically**: re-reading the export yields the same values as the
  source. Two normalisations are intentional. Container numbers are written canonically (`ABCU1234567`,
  no dash, upper case), and text cells are trimmed (e.g. a seal of `" -"` becomes `"-"`).
- The `Code` column is stored as an opaque batch reference per event and is used to export the same batch back.
- **Backfill mode** creates missing lines, depots, body states, bookings, B/Ls, containers and cycles, and skips
  the empty-return prerequisites (BR-L2). Every such action is a row warning. Without backfill, missing references are errors.
- Import is a preview unless committed, and all-or-nothing unless "import valid rows only" is chosen.
