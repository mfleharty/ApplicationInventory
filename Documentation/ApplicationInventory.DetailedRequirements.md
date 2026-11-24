# Application Inventory — Detailed Business & Functional Requirements (Custom Build)

**Version:** 0.1 (draft)
**Date:** 2026-08-05
**Author:** Claude (acting BA), for Mark Fleharty / Assurity Life
**Extends:** ApplicationInventory.Requirements.docx v1.2.1 (Bartunek/Fleharty/Burns, 2026-01-26)

## 0. Purpose of this document

The original SRS (v1.2.1) defines *goals* and *evaluation-level* requirements (FO/BR/FR/NFR/AGR) — appropriate for comparing vendor tools. Now that the decision is to **custom build**, this document goes one level deeper: concrete entities, field-level data model, module-by-module functional requirements, business rules, and acceptance criteria — the level of detail a dev team can actually build against.

Every requirement below traces back to an original ID where one exists (shown as `→ FR-05` etc.). New requirements introduced because of custom-build specifics (things a vendor tool would give you for free but that we now have to design ourselves) are marked **[NEW]**.

---

## 1. Actors / Personas

| Actor | Description | Primary needs |
|---|---|---|
| **Application Owner** | Individual or team accountable for an application | Register/maintain their app's record, low friction |
| **Architect / Governance Lead** | Reviews portfolio, drives standards | Search, reporting, flag gaps, approve promotions |
| **Developer / SME** | Builds/maintains the app day to day | Link repos/pipelines, keep metadata current |
| **Security & Compliance** | Risk and audit function | Identify PII-serving, public-facing, high-risk apps |
| **Executive / Leadership** | Consumes portfolio metrics | Dashboards, trend reporting |
| **Catalog Administrator** | Owns the inventory system itself | Manage schema, roles, data quality enforcement |
| **External systems** (Entra/AD, CI/CD, APM) | Integrate, don't own data | Push/pull metadata via API |

---

## 2. Guiding Principles (carried forward, unchanged)

1. Application-centric, not tool-centric
2. Single source of truth — one system owns identity/core metadata
3. Low-friction adoption — partial data acceptable until promoted to Active
4. Extensibility — schema must grow without a rebuild

---

## 3. Domain Model

### 3.1 Entity Overview

```
Team ──owns──► Application ◄──has many── ApplicationRelationship (self-referencing)
  │                  │  ▲
  │                  │  └──has many── ApplicationInstance (Phase 2)
Person ──SME/Owner of──┘
  │
  └──sourced from── Entra/AD (integration)

Application ──has many── CustomMetadataField (extensible)
Application ──has many── AuditLogEntry
Application ──has many── NotificationSubscription
```

### 3.2 Entity Definitions

#### Application (core entity)
| Field | Type | Required (MVP) | Source | Notes |
|---|---|---|---|---|
| ApplicationId | GUID | system | system | immutable |
| Name | string | **Yes** | manual | unique |
| Aliases | string[] | No | manual | historical/alt names |
| Description / Purpose | text | Should | manual | supports BR-01 discoverability |
| ApplicationType | enum | **Yes** | manual | Web App, API/Service, Background Job, Scheduled Task, Windows Service, Desktop App, Vendor-Managed, 3rd-Party → `FR-03` |
| OwnerTeam | FK→Team | **Yes** (by promotion gate) | manual | → `FR-02`, `BR-02` |
| BusinessOwner | FK→Person | Should | manual | accountable individual — split from OwnerTeam per decision log §9, item #2 |
| TechnicalOwner | FK→Person | Should | manual | |
| SMEs | FK→Person[] w/ Role | No | manual | → §9.3.1.2 wishlist |
| LifecycleStatus | enum | **Yes**, defaults Proposed | manual | Proposed/In Development, In Testing, Active, Deprecated, Decommissioned → `FR-04` |
| Criticality | enum | Should (**Yes** to reach Active) | manual | Critical, High, Medium, Low → `BR-04`; self-declared by owner at MVP, no formal scoring rubric (decision log §9, item #3) |
| HostingEnvironment | enum[] | Should | manual/integration | On-prem, Azure, Hybrid, Vendor-hosted |
| Frameworks / Languages | tag[] | No | manual | |
| PubliclyExposed | bool | Should | manual | security flag |
| ServesPII | bool | Should | manual | security flag |
| SecurityRiskNotes | text | No | manual | free text initially, not a scored rubric (see Open Question #3) |
| DocumentationLinks | url[] | No | manual | |
| RepositoryLinks | url[] | Should | manual/integration | → `FR-10` |
| CI/CD PipelineLinks | url[] | Should | manual/integration | → `FR-10` |
| DeploymentMechanism | enum | No | manual | Azure DevOps, OpenShift, Jenkins, Manual, Other |
| ReleaseLink | url | No | manual | |
| OriginalProdReleaseDate | date | No | manual/integration | consider deriving from CI/CD later |
| MostRecentProdReleaseDate | date | No | integration (future) | |
| CreatedDate / CreatedBy | datetime/FK | system | system | |
| UpdatedDate / UpdatedBy | datetime/FK | system | system | |
| DataCompletenessFlag | computed | system | system | drives `AGR-02` |

#### ApplicationRelationship
| Field | Type | Notes |
|---|---|---|
| SourceApplicationId | FK→Application | |
| TargetApplicationId | FK→Application | |
| RelationshipType | enum | Depends On, Calls API Of, Shares Database With, Replaces/Replaced By |
| Description | text | |
| Source | enum | Manual, Automated *(automated discovery is out-of-scope for v1 — flag stays Manual for now)* |
| CreatedDate/By | system | |

→ `FR-05`. **Flag:** FR-05 is Must-have but automated dependency discovery is explicitly out-of-scope (§3.2 of original doc). This data will be manually declared and will decay exactly like the data described in the problem statement unless a review/attestation cadence is built into the workflow (see BR-Rule 3 in §6 below).

#### Team / Person
| Team | Person |
|---|---|
| TeamId, Name, DistributionList/Contact | PersonId, Name, Email, EmployeeId *(from Entra)*, Role, ExpertiseLevel, OnCallNumber (optional) |

→ `FR-02`, integrates with `NFR-03` (Entra/AD).

#### CustomMetadataField **[NEW]**
Supports `FR-09` (extensible metadata) without a schema migration for every new field.
| Field | Type |
|---|---|
| FieldKey | string |
| DisplayLabel | string |
| DataType | enum (text, number, bool, date, enum-list) |
| AppliesToApplicationId | FK (nullable = applies to all) |
| Value | polymorphic |

#### AuditLogEntry **[NEW]**
| Field | Type |
|---|---|
| EntityType / EntityId | string / GUID |
| FieldChanged | string |
| OldValue / NewValue | string |
| ChangedBy / ChangedDate | FK / datetime |

→ `NFR-04`.

#### NotificationSubscription **[NEW]**
| Field | Type |
|---|---|
| EventType | enum (NewApplicationCreated, OwnershipChanged, LifecycleChanged, PromotionBlocked, DataQualityReminder) |
| Recipient | FK→Person or Role |
| Channel | enum (Email, Teams) |

→ `FR-11`.

#### ApplicationInstance *(Phase 2 — deferred, confirmed)*
Server/environment-level detail from §9.3.1.3–9.3.1.4 of the original doc (DNS, cert expiration, load balancer, DB user, etc.). **Confirmed excluded from MVP schema** (decision log §9, item #4). This is CMDB-grade detail, several of the fields (Database User) risk becoming a secrets-management anti-pattern if implemented naively, and §8.3 of the original doc already calls for pruning to "what's realistic." Revisit once core catalog adoption (6-month success criteria) is met — likely alongside the SIEM-replacement idea noted in the original doc's closing note, since that's the one thing that would justify the added complexity.

---

## 4. Functional Requirements by Module

### 4.1 Registration & Catalog Management (`REG`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| REG-01 | User can create an Application record with only Name + Type populated | Must | `FR-01`, `AGR-01` |
| REG-02 | New records default to LifecycleStatus = Proposed and DataCompletenessFlag = Incomplete | Must | `AGR-01` |
| REG-03 | User can edit any field on an Application they own; Admins can edit any record | Must | `FR-01` |
| REG-04 | System prevents duplicate Application names (warns, doesn't hard-block — allows override with justification) | Should | `BR-06` |
| REG-05 | Records support soft-delete only (see BR-Rule 5); no hard delete once Active | Must | `NFR-04` |

### 4.2 Ownership & Accountability (`OWN`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| OWN-01 | Assign OwnerTeam and/or BusinessOwner/TechnicalOwner to an Application | Must | `FR-02`, `BR-02` |
| OWN-02 | Owner lookup is backed by Entra/AD directory (typeahead, not free text) | Should | `NFR-03` |
| OWN-03 | System flags records with no owner assigned in reports and on record detail view | Must | `AGR-02` |
| OWN-04 | Ownership changes are captured in the audit log with old/new owner | Must | `NFR-04` |

### 4.3 Classification (`CLS`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| CLS-01 | Assign one ApplicationType from a controlled enum list | Must | `FR-03` |
| CLS-02 | Admins can extend the ApplicationType enum without a code deploy | Should | `FR-09` |
| CLS-03 | Free-text tagging supplements ApplicationType for cross-cutting labels (e.g., "vendor-managed," "PII") | Should | `FR-09` |

### 4.4 Lifecycle Management (`LIF`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| LIF-01 | LifecycleStatus enum: Proposed/In Dev, In Testing, Active, Deprecated, Decommissioned | Must | `FR-04`, `BR-03` |
| LIF-02 | Transition to **Active** is blocked unless Owner, Criticality, and Classification are populated (promotion gate) | Must | `AGR-03` |
| LIF-03 | Transition to **Decommissioned** sets record read-only except for Admins | Must | *(new, supports NFR-04 integrity)* |
| LIF-04 | Lifecycle changes trigger notification per §4.10 | Should | `FR-11` |
| LIF-05 | Lifecycle history (not just current state) is retained and viewable | Should | `BR-03` |

### 4.5 Relationships & Dependencies (`REL`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| REL-01 | Define directional relationships between two Applications with a RelationshipType | Must | `FR-05` |
| REL-02 | View a dependency graph (upstream/downstream) for a given Application, at least 1 hop; stretch: full chain traversal | Must (1-hop) / Should (full chain) | `FR-05` |
| REL-03 | Relationship records require both endpoints to be existing Application records (referential integrity) | Must | *(new — data integrity)* |
| REL-04 | Relationships are periodically flagged for owner re-attestation (e.g., every 6 months) since discovery is manual | Should | *(new — mitigates staleness risk called out in §3.2 above)* |

### 4.6 Search, Filtering & Discovery (`SRCH`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| SRCH-01 | Full-text search across Name, Aliases, Description | Must | `FR-06`, `BR-01` |
| SRCH-02 | Filter by Type, LifecycleStatus, Criticality, OwnerTeam, PubliclyExposed, ServesPII | Must | `FR-06` |
| SRCH-03 | Saved filters/views per user | Nice | *(new — usability)* |
| SRCH-04 | Search results return in ≤2s at catalog sizes up to 2,000 records | Must | `NFR-01` (concrete target) |

### 4.7 Extensible Metadata (`META`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| META-01 | Admins define new custom fields (type, label, applicability) without a schema migration | Should | `FR-09` |
| META-02 | Custom fields appear on record forms and are included in search/filter and export | Should | `FR-09` |

### 4.8 Programmatic Access / API (`API`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| API-01 | REST API for CRUD on Application records | Should | `FR-07` |
| API-02 | API supports the same RBAC as the UI (no back-door access) | Must (once API-01 exists) | `NFR-02`, `NFR-06` |
| API-03 | API is documented (OpenAPI/Swagger) | Should | `FR-07` |
| API-04 | Read-only export endpoint (CSV/JSON) for reporting tools | Should | `FR-07` |

### 4.9 Bulk Import / Export (`IMP`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| IMP-01 | Bulk import via CSV template with validation + error report | Should | `FR-08` |
| IMP-02 | Import supports upsert (match on Name or an external ID) to avoid duplicate creation on re-run | Should | *(new)* |
| IMP-03 | Bulk export of full catalog (all fields, including custom metadata) | Should | `FR-07` |

### 4.10 Notifications (`NOTIF`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| NOTIF-01 | Notify designated architect/manager role when a new Application record is created | Nice | `FR-11` |
| NOTIF-02 | Notify Owner when their record is flagged Incomplete after N days | Should | `AGR-02` |
| NOTIF-03 | Notify on lifecycle transitions (esp. into Decommissioned) | Nice | *(new)* |

### 4.11 Reporting & Dashboards (`RPT`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| RPT-01 | Executive dashboard: % registered, % with owner, breakdown by lifecycle/criticality | Should | `BR-05` |
| RPT-02 | Report of records missing owner and/or classification | Must | `AGR-02` |
| RPT-03 | Report identifying potentially redundant/overlapping applications (manual tagging-assisted, not automatic detection) | Nice | `BR-06` |
| RPT-04 | Report flagging apps needing upgrade/modernization based on lifecycle age or tech currency tags | Should | `BR-07` |
| RPT-05 | Progress-to-goal view tracking the 4/6/12-month success criteria directly | Should | *(new — makes the SRS's own success criteria self-reporting)* |

### 4.12 Data Quality & Governance Workflow (`GOV`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| GOV-01 | Incomplete records are visibly flagged (list + record-level banner), never hidden or blocked from creation | Must | `AGR-01`, `AGR-02` |
| GOV-02 | Standards enforcement (required fields, promotion gates) is configurable per lifecycle stage, not hardcoded — enables "gradual enforcement" without a code change | Must | `AGR-03` |
| GOV-03 | Admin can bulk-message/flag a cohort of owners (e.g., "these 40 apps are missing criticality") | Nice | *(new)* |

### 4.13 Integrations (`INTG`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| INTG-01 | Identity/directory integration with Entra/AD for Person/Team lookups and SSO | **Must (MVP)** | `NFR-03` |
| INTG-02 | Link-out integration with source control (repo URL validation, maybe metadata pull like last-commit date) | Should | `FR-10`, `AGR-04` |
| INTG-03 | Link-out integration with CI/CD (Azure DevOps pipeline URL, maybe last-deployment timestamp pull) | Should | `FR-10`, `AGR-04` |
| INTG-04 | APM/monitoring integration | Out of scope v1 | *(explicitly excluded — §3.2 original doc)* |

### 4.14 Audit Trail (`AUD`)
| ID | Requirement | Priority | Traces to |
|---|---|---|---|
| AUD-01 | Every field-level change to an Application record is logged with actor, timestamp, old/new value | Should | `NFR-04` |
| AUD-02 | Audit log is viewable per-record (change history tab) and exportable | Should | `NFR-04` |
| AUD-03 | Audit log retention = 1 year. No regulatory driver identified as of 2026-08-05 (decision log §9, item #7); revisit if Compliance/Legal later identifies one (e.g., NY DFS) | Should | `NFR-04` |

---

## 5. Non-Functional Requirements (concrete targets)

| ID | Requirement | Target | Traces to |
|---|---|---|---|
| NFR-01 | Scale | 500 apps at launch, designed to 2,000+ without re-architecture | `NFR-01` |
| NFR-02 | RBAC | Roles: Admin, Owner/Editor (scoped to owned apps), Viewer (org-wide read), API Consumer (read-only), Auditor (read + audit log) | `NFR-02` |
| NFR-03 | Identity | SSO via Entra/AD — **required at MVP**, not fast-follow (decision log §9, item #6) | `NFR-03` |
| NFR-04 | Auditability | All writes logged; audit log itself is append-only | `NFR-04` |
| NFR-05 | Availability | 99.5% during business hours (catalog, not a runtime-critical system) | `NFR-05` |
| NFR-06 | Security | TLS in transit, encryption at rest for any sensitive fields, no secrets ever stored in metadata fields (see BR-Rule below) | `NFR-06` |
| NFR-07 | Deployment | On-prem virtual servers; database is a new SQL Server database, likely on existing SQL Server infrastructure; deployed via Azure DevOps pipeline (decision log §9, item #5) | `NFR-07` |
| NFR-08 | Performance | Search/list views ≤2s response at target scale | *(new, concretizes NFR-01)* |
| NFR-09 | Usability | Minimum-viable record creation completable in <2 minutes | *(new — directly supports the "low friction adoption" principle)* |

---

## 6. Business Rules

1. **BR-Rule 1** — An Application record can be created with only Name + Type. No other field is required at creation. (`AGR-01`)
2. **BR-Rule 2** — Any record missing Owner or Classification is auto-flagged "Needs Attention" and appears in the GOV-02 report. (`AGR-02`)
3. **BR-Rule 3** — Promotion to LifecycleStatus = Active requires Owner + Criticality + Classification (the "gate"). Relationships (dependencies) are **not** gated at promotion but are subject to periodic re-attestation reminders (`REL-04`) since automated discovery is out of scope.
4. **BR-Rule 4** — Decommissioned applications become read-only except to Admins, but remain searchable.
5. **BR-Rule 5** — Hard delete is disallowed for any record that has ever reached Active status; use Decommissioned instead. Records still in Proposed status may be deleted outright.
6. **BR-Rule 6** — Relationship records must reference two existing Application records; no orphaned references.
7. **BR-Rule 7 (guardrail)** — No field in the system may store secrets/credentials in plain form. The original wishlist (§9.3.1.3) includes a "Database User" field; if password-adjacent data is ever needed, it must reference a secrets vault (e.g., Azure Key Vault) by name/pointer only, never store the value.

---

## 7. Representative User Stories & Acceptance Criteria

**US-1** — *As an Application Owner, I want to register a new application with minimal fields, so it's tracked without needing a perfect record on day one.*
- AC: Given I'm authenticated, when I submit Name + Type, then a record is created with LifecycleStatus = Proposed and flagged Incomplete.

**US-2** — *As an Architect, I want to filter applications by criticality and lifecycle state, so I can build a portfolio report.*
- AC: Given ≥1 record exists, when I filter Criticality=Critical AND LifecycleStatus=Active, then only matching records return in ≤2s.

**US-3** — *As a Security reviewer, I want to see which applications are public-facing and serve PII, so I can prioritize risk reviews.*
- AC: Given records have PubliclyExposed and ServesPII populated, when I filter on both = true, then I get a prioritized risk list.

**US-4** — *As a Developer, I want to link my application to its repo and pipeline, so deployment provenance is traceable.*
- AC: Given I own an Application record, when I add a RepositoryLink and PipelineLink, then both are validated as well-formed URLs and shown on the record.

**US-5** — *As an Architect, I want to be notified when a new application is registered, so I can review it early.*
- AC: Given NOTIF-01 subscription exists for my role, when a record is created, then I receive an email/Teams notification within 5 minutes.

**US-6** — *As an Executive, I want a dashboard tracking % of apps with ownership/lifecycle defined, so I can monitor progress toward the 6- and 12-month goals.*
- AC: Given the catalog has N records, the dashboard shows live % with Owner assigned and % with LifecycleStatus defined, matching the SRS §7 success criteria definitions exactly.

---

## 8. Assumptions & Constraints

- Entra/AD is available and permissioned for lookups (OWN-02, INTG-01).
- The existing "Assurity Application Inventory" (referenced in original doc §9.1/§8.2) will be assessed for data reuse, not assumed authoritative — migration approach TBD after §8.2 validation work.
- **Custom build implication:** unlike Backstage/Compass, none of the CRUD/search/RBAC scaffolding is pre-built — the team owns hosting, patching, and integration maintenance long-term. Confirm this trade-off is still preferred now that it's concrete (control/fit vs. build+maintenance cost).
- Out of scope for v1 (carried forward): cost allocation, runtime performance monitoring, SLA enforcement, automated dependency discovery, app rehosting/refactoring.
- Out of scope for v1 (new, custom-build-specific): `ApplicationInstance` server/environment-level detail (§3.2 above) — recommend Phase 2.

---

## 9. Decisions Log

All seven items originally raised as open questions are resolved as of 2026-08-05. Kept here (rather than deleted) so the rationale stays attached to the requirement.

| # | Question | Decision | Rationale |
|---|---|---|---|
| 1 | Who administers the catalog itself (schema, RBAC, data-quality enforcement)? | **Architecture team**, shared ownership | Matches who already drives standards elsewhere in the SRS; avoids a single-person bus-factor risk |
| 2 | Single Owner field or split? | **Split** into `BusinessOwner` + `TechnicalOwner` | `BR-02` asks for ownership *and* support contacts — one field can't represent both cleanly; cheap to add now vs. a schema change later |
| 3 | Criticality: self-declared or rubric? | **Self-declared** by app owners at MVP; no formal scoring rubric yet | Matches low-friction adoption principle and `AGR-03`'s gradual-enforcement approach; a rubric can layer on top later without a schema change |
| 4 | Defer `ApplicationInstance` (server/cert/DB fields) to Phase 2? | **Confirmed deferred** | Not required by any Must-have; several fields (e.g., Database User) risk a secrets-management anti-pattern if built naively |
| 5 | Where is the app hosted? | **On-prem virtual servers**, using existing SQL Server infrastructure with a likely new dedicated database | Real infra constraint — supersedes the original doc's Azure App Service / OpenShift framing; drives the tech-stack conversation next (task #3) toward a stack that fits on-prem VM + SQL Server hosting (e.g., IIS/.NET) rather than PaaS-native tooling |
| 6 | Entra/AD SSO: MVP or fast-follow? | **MVP** — built in from day one | Avoids standing up a throwaway local-auth system that has to be ripped out later; `INTG-01` and `NFR-03` upgraded to Must for MVP |
| 7 | Audit log retention — compliance driver? | **1 year**, no known regulatory driver | No NY DFS or similar requirement identified currently; revisit if Compliance/Legal later flags one |
| 8 | OS/runtime environment for the on-prem VMs (follow-up to #5)? | **Windows Server VMs, no container runtime in use** | Confirms a traditional IIS-hosted deployment model rather than containers; directly shapes the tech-stack recommendation (task #3) toward a .NET/IIS-native stack |

**Net effect on tech-stack direction:** Windows Server VMs + SQL Server + no containers points strongly toward an **ASP.NET Core app hosted under IIS**, using Entity Framework Core against the new SQL Server database, with Entra ID (Azure AD) integration for SSO via a standard OIDC/MSAL flow — a well-trodden path on this exact infrastructure combination. This is a recommendation to validate in task #3, not a final decision.

---

## 10. Traceability Summary

Every `FO/BR/FR/NFR/AGR` ID from the original SRS is referenced at least once above via "Traces to" columns. Items introduced here that have no original ID are marked **[NEW]** and generally fall into one of three categories: (a) data-integrity/security guardrails a vendor tool would have handled implicitly (BR-Rule 5, BR-Rule 7, REL-03), (b) concrete numeric targets for previously qualitative NFRs (NFR-08, NFR-09), or (c) workflow details needed to make "gradual enforcement" (`AGR-03`) actually implementable (LIF-02, GOV-02, REL-04).
