# Capability snapshot boundary (Phase 3A)

`CapabilitySnapshotV1` is a complete, independently revisioned capability contract. Its catalog version is the explicit local constant `yaqoot-capabilities-1`. A future Control Panel producer must emit a new revision only when the effective module/feature state changes; it must not create a snapshot or delivery for a no-op configuration change.

All eight modules and all 51 features are required. Core (`M01_CORE`) and its features must be enabled. Offers (`M05_OFFERS`) may be represented in configuration, but the evaluator continues to return `NotImplemented`, so a snapshot cannot activate Offers behavior.

Canonical JSON sorts modules and features by stable code, and its SHA-256 digest is used for equal-revision and delivery-id comparisons. Capability revisions and tables are separate from lifecycle state.

The apply boundary is transport-neutral for future authenticated push and pull adapters. It commits snapshot, receipt, and checkpoint state under a PostgreSQL transaction and advisory lock, then publishes a new immutable runtime reference after commit. Request-time evaluation never queries PostgreSQL. Before a valid snapshot is loaded, the provider preserves the Phase 2 permissive baseline; after one is loaded, storage or Control Panel outages retain the last in-memory state.

Phase 3A intentionally adds no endpoint, authentication mechanism, controller/filter enforcement, UI, plan, entitlement, or Control Panel implementation.
