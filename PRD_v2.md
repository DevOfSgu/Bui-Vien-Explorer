# Product Requirements Document (PRD) v2

## 1. Document Control

| Field | Value |
|---|---|
| Product Name | TravelSystem Mobile - Bui Vien Explorer |
| Version | v2.0 |
| Date | 2026-04-05 |
| Owner | Product + Engineering Team |
| Status | Draft for internal review |
| Platforms | .NET MAUI Mobile, ASP.NET Core 10 Web (Admin/Vendor), SQL Server |

## 2. Executive Summary

TravelSystem is a tour-based urban walking guide optimized for dense, noisy GPS environments like Bui Vien. The product combines:
- Mobile offline-first experience (tour discovery, navigation, favorites, narration).
- Web admin/vendor operations (content moderation, zone management, analytics heatmap).

The main business outcome is to improve on-site tourist engagement while giving admins and vendors operational control over POI content quality and updates.

## 3. Problem Statement

Users in dense urban streets face three recurring issues:
- Unstable GPS causes false trigger or missed trigger at POI boundaries.
- Narration content quality can degrade if moderation/sync is inconsistent.
- Operators need clear analytics to understand where users actually enter zones.

This PRD v2 focuses on stable geofence behavior, moderation reliability, and analytics clarity.

## 4. Goals and Non-Goals

### 4.1 Goals
- Provide stable POI auto-selection and narration trigger under real-world GPS noise.
- Support full content lifecycle: vendor submit/resubmit -> admin approve/reject -> mobile refresh.
- Provide heatmap analytics for zone popularity using EnterZone events.
- Keep offline-first UX responsive for favorites and local rendering.

### 4.2 Non-Goals (Current Release)
- User account/login for tourists (guest flow remains anonymous).
- Full route-geometry-based movement analytics from ping streams in admin heatmap.
- Advanced role matrix per vendor-zone assignment (current model is vendor by shop scope).

## 5. Personas and Jobs-to-be-Done

### 5.1 Tourist (Guest)
- Wants fast tour discovery and smooth map guidance.
- Wants accurate audio trigger when near POI.
- Wants to save POIs even with poor network.

### 5.2 Vendor
- Wants to submit and update Vietnamese source script for POIs under owned shop.
- Wants transparent moderation status and quick correction loop.

### 5.3 Admin
- Wants to moderate narration content and manage multilingual outputs.
- Wants global heatmap to identify popular zones from EnterZone events.

## 6. Scope

### 6.1 In Scope
- Mobile:
- Tour list/detail, map rendering, location tracking stream + poll fallback.
- Geofence auto-select with debounce, hysteresis, cooldown.
- Audio popup flow with narration refresh and pending queue behavior.
- Favorites local-first with background sync.
- Web Admin:
- Narration moderation (approve/reject), details, manage per zone language set.
- Heatmap global rendering by analytics coordinates (EnterZone-only mode).
- Web Vendor:
- Create/edit Vietnamese source script for zones owned by vendor shop.
- Filter and pagination for script listing.

### 6.2 Out of Scope
- Predictive recommendation engine.
- Dynamic pricing, payment, booking.
- Enterprise IAM/SSO.

## 7. Product Requirements

### 7.1 Functional Requirements (FR)

| ID | Requirement | Priority | Acceptance Criteria |
|---|---|---|---|
| FR-01 | Mobile must track user location using stream-first with polling fallback. | P0 | When stream is stale, app polls location and continues map updates without freezing. |
| FR-02 | Mobile must auto-select nearest zone only when geofence conditions pass stability checks. | P0 | Auto-select requires distance within trigger radius plus debounce/sample checks before trigger. |
| FR-03 | Mobile must avoid rapid zone flip-flop near boundaries. | P0 | Exit confirmation requires hysteresis + outside-sample confirmation before releasing current zone. |
| FR-04 | Mobile must trigger narration UI on selected stop and avoid dropped triggers while popup busy. | P0 | If narration is in progress, next stop is queued and replayed after current flow ends. |
| FR-05 | Mobile must block narration when POI is outside operating hours. | P1 | Closed-hours popup shown; no narration playback starts. |
| FR-06 | Vendor can create/edit Vietnamese source script for owned zones only. | P0 | Unauthorized zone operations are denied; submit status becomes Pending. |
| FR-07 | Admin can approve/reject narrations and return to same moderation page with success feedback. | P1 | Approve/reject redirects to return URL and shows success modal. |
| FR-08 | Heatmap displays global zone popularity from EnterZone analytics only. | P1 | Heatmap draws using EnterZone-derived points; no LocationPing or PlayNarration weighting in current mode. |
| FR-09 | Admin narration list supports language filtering with state preserved across pagination. | P1 | Language filter modifies query result and persists while paging. |
| FR-10 | Favorites must work in offline mode and sync later when online. | P1 | Favorite action updates local store immediately and sync attempts occur when connectivity returns. |

### 7.2 Business Rules
- Vendor ownership is shop-scoped: one vendor manages zones belonging to assigned shop.
- Each zone has one source narration in Vietnamese; translated narrations are managed by admin workflow.
- Heatmap in current release is interpreted as zone entry popularity, not full movement density.

## 8. Current Technical Baseline (As Implemented)

### 8.1 Mobile Geofence and GPS Parameters
- Trigger radius fallback: 45m.
- Practical minimum radius: 28m.
- Exit hysteresis factor: 1.25x plus accuracy compensation.
- Debounce:
- Normal auto-select: 1.2s.
- Switch between stops: 1.8s.
- Cooldown for switching: 2.5s.
- Location freshness:
- Max accepted location age: 5s.
- Fallback auto-select age window: 3s.
- Location ping interval:
- Normal: 60s.
- Simulation: 8s.

### 8.2 Key Mobile Events
- EnterZone: emitted on zone entry trigger (deduplicated per session context except simulation pass behavior).
- LocationPing: periodic location telemetry.
- PlayNarration|<lang>: emitted from audio popup playback flow.

### 8.3 Backend and Portal
- APIs currently used by mobile include tours, zones, narrations, analytics, favorites.
- Admin has narration moderation and heatmap pages.
- Vendor has narration authoring page with zone ownership checks.

## 9. Non-Functional Requirements (NFR)

| ID | Requirement | Target |
|---|---|---|
| NFR-01 | Map interaction responsiveness | UI remains interactive during foreground tracking cycles |
| NFR-02 | Reliability under weak GPS | Auto-select should resist jitter and boundary ping-pong |
| NFR-03 | Offline continuity | Favorites and cached narration access continue without network |
| NFR-04 | Data integrity | Moderation status transitions must be persisted atomically |
| NFR-05 | Observability | Diagnostic logs must capture reason codes for geofence/audio decisions |

## 10. Analytics and Reporting

### 10.1 Heatmap Semantics (Current)
- Primary metric: EnterZone occurrence density.
- Intended interpretation: "where users most often enter zones".
- Not intended: precise route trajectory reconstruction.

### 10.2 Future Optional Mode
- Add filter mode toggle:
- EnterZone only.
- LocationPing only.
- Combined weighted mode.

## 11. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| GPS drift in dense urban environment | False positive/negative triggers | Keep hysteresis/debounce logic and reason-code logging enabled in debug builds |
| Runtime mismatch between source and deployed backend | Users see stale behavior after fixes | Enforce clean restart and release checklist per deployment |
| Hardcoded tunnel base URL in app constants | Wrong environment data source | Introduce environment-driven API base URL config before release hardening |
| Moderation bottleneck | Delayed content freshness | Keep return-to-same-page moderation UX and notifications |

## 12. Dependencies
- Azure Translator and Azure Speech configuration for translation/TTS generation.
- SQL Server availability for web backend.
- Android location permissions and device-level GPS quality.

## 13. Rollout and Validation Plan

### 13.1 Validation Checklist
- Build mobile debug APK and run field tests for:
- Outside zone false trigger case.
- Pass-through zone no-trigger case.
- Re-entry replay case.
- Pull gps_map_trace.log and verify reason-code sequences.

### 13.2 Release Gate
- No blocking errors in mobile/web build.
- Moderation flow and language filter verified in admin UI.
- Heatmap confirmed to render EnterZone-only data.

## 14. Open Questions
- Should re-entry into the same zone replay narration after explicit exit timeout?
- Should EnterZone deduplication reset by time window, distance window, or session boundary?
- Should vendor authorization evolve from shop-level scope to explicit vendor-zone mapping?

## 15. Change Log

| Version | Date | Changes |
|---|---|---|
| v1.x | 2026-03 | Initial PRD draft content |
| v2.0 | 2026-04-05 | Standardized structure, aligned geofence parameters, clarified EnterZone-only heatmap semantics, added acceptance criteria and risk model |
