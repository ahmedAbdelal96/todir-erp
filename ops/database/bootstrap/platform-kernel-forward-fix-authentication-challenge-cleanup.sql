-- Authentication challenges are disposable pre-auth state. Removing an
-- identity must remove its outstanding challenges as part of the same delete.

ALTER TABLE platform_iam.auth_challenge
    DROP CONSTRAINT IF EXISTS auth_challenge_identity_id_fkey;
ALTER TABLE platform_iam.auth_challenge
    ADD CONSTRAINT auth_challenge_identity_id_fkey
    FOREIGN KEY (identity_id) REFERENCES platform_iam.identity (id) ON DELETE CASCADE;
