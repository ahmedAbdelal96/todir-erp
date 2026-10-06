# Transaction coordination

Cross-module invariants share one PostgreSQL connection, one transaction, and one tenant context. Participating commands enlist their database work and add outbox rows before commit. External APIs and brokers are not part of the transaction. A rollback removes both business writes and integration intents.
