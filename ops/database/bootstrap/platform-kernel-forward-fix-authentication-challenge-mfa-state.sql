-- Carry only the completed MFA state between the MFA and organization steps.
-- This is server-owned state, not a client assertion or credential.

ALTER TABLE platform_iam.auth_challenge
    ADD COLUMN IF NOT EXISTS mfa_verified boolean NOT NULL DEFAULT false;
