# Module lifecycle

Activation and deactivation are explicit idempotent operations. Provisioning is resumable and never deletes tenant data. Hard dependents block deactivation; retained data enters WindDown or ReadOnly as appropriate. Stateful modules expose go-live inputs and reconciliation checks before becoming Active.
