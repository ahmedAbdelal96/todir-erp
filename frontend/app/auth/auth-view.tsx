import { useRef, useState, type FormEvent, type ReactNode } from "react";
import { loginUser, changeInitialPassword, verifyMfa, type AuthNextStep } from "../../core/api/auth-service.ts";
import { ApiError } from "../../core/api/http-client.ts";
import { Button } from "../../components/primitives/button.tsx";
import { Alert } from "../../components/primitives/alert.tsx";
import { PasswordField } from "../../components/fields/index.ts";

export interface AuthViewProps {
  readonly language?: "ar" | "en";
  readonly onAuthenticated: (email: string) => void;
}

type AuthStep = "login" | "initial-password" | "mfa";

interface DevelopmentLoginProfile {
  readonly label: string;
  readonly email: string;
}

const developmentLoginPassword = import.meta.env.DEV ? import.meta.env.VITE_DEV_LOGIN_PASSWORD : undefined;
const developmentLoginProfiles: readonly DevelopmentLoginProfile[] = developmentLoginPassword
  ? [
      { label: "Atlas — مسؤول النظام", email: "admin@atlas.com" },
      { label: "Delta — مسؤول النظام", email: "admin@delta.com" }
    ]
  : [];

export function AuthView({ language = "ar", onAuthenticated }: AuthViewProps): React.JSX.Element {
  const [step, setStep] = useState<AuthStep>("login");
  const [challengeToken, setChallengeToken] = useState<string>("");
  const [error, setError] = useState<string | undefined>();
  const [submitting, setSubmitting] = useState(false);

  // Login form state
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const authenticatedEmailRef = useRef("");

  // Initial password state
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  // MFA state
  const [mfaCode, setMfaCode] = useState("");

  const isArabic = language === "ar";
  const direction = isArabic ? "rtl" : "ltr";

  const t = {
    brand: "TODIR",
    loginTitle: isArabic ? "تسجيل الدخول" : "Sign in",
    loginSubtitle: isArabic ? "أدخل بريدك الإلكتروني وكلمة المرور للوصول إلى حساب شركتك" : "Enter your email and password to access your company workspace",
    emailLabel: isArabic ? "البريد الإلكتروني" : "Email address",
    emailPlaceholder: isArabic ? "name@company.com" : "name@company.com",
    passwordLabel: isArabic ? "كلمة المرور" : "Password",
    showPassword: isArabic ? "عرض" : "Show",
    hidePassword: isArabic ? "إخفاء" : "Hide",
    signInButton: isArabic ? "تسجيل الدخول" : "Sign in",
    signingIn: isArabic ? "جارٍ تسجيل الدخول..." : "Signing in...",
    developmentAccountsTitle: isArabic ? "حسابات الاختبار السريعة" : "Quick development accounts",
    developmentAccountsHint: isArabic ? "متاحة في بيئة التطوير فقط لتسريع الاختبار المحلي." : "Available only in development to speed up local testing.",

    // Initial password strings
    initPasswordTitle: isArabic ? "تعيين كلمة المرور الجديدة" : "Set New Password",
    initPasswordSubtitle: isArabic ? "مرحبًا بك! كإجراء أمني عند تسجيل الدخول لأول مرة، يرجى تعيين كلمة مرور جديدة لحسابك." : "Welcome! For security, please set a new password for your account on first sign in.",
    newPasswordLabel: isArabic ? "كلمة المرور الجديدة" : "New password",
    confirmPasswordLabel: isArabic ? "تأكيد كلمة المرور الجديدة" : "Confirm new password",
    passwordRequirements: isArabic ? "12 حرفًا على الأقل، تتضمن حروفًا كبيرة وصغيرة وأرقامًا ورموزًا خاصة." : "At least 12 characters, including uppercase, lowercase, numbers, and symbols.",
    savePasswordButton: isArabic ? "حفظ كلمة المرور والمتابعة" : "Save password & continue",
    savingPassword: isArabic ? "جارٍ حفظ كلمة المرور..." : "Saving password...",
    passwordsDoNotMatch: isArabic ? "كلمة المرور وتأكيدها غير متطابقين." : "Passwords do not match.",

    // MFA strings
    mfaTitle: isArabic ? "التحقق من هويتك" : "Verify Your Identity",
    mfaSubtitle: isArabic ? "أدخل رمز التحقق المكون من 6 أرقام من تطبيق المصادقة (Authenticator) على هاتفك للمتابعة." : "Enter the 6-digit verification code from your authenticator app to proceed.",
    mfaCodeLabel: isArabic ? "رمز التحقق" : "Verification code",
    mfaCodePlaceholder: "123456",
    verifyButton: isArabic ? "تحقق والدخول" : "Verify & proceed",
    verifying: isArabic ? "جارٍ التحقق..." : "Verifying...",
    backToLogin: isArabic ? "العودة لتسجيل الدخول" : "Back to sign in",

    // Error messages
    defaultError: isArabic ? "تعذر إكمال العملية. يرجى المحاولة مرة أخرى." : "Operation could not be completed. Please try again.",
    invalidCredentials: isArabic ? "البريد الإلكتروني أو كلمة المرور غير صحيحة." : "Invalid email or password.",
    accountSuspended: isArabic ? "تم تعليق هذا الحساب. يرجى التواصل مع إدارة شركتك." : "This account is suspended. Please contact your company administrator.",
    accountLocked: isArabic ? "تم قفل الحساب مؤقتًا لكثرة المحاولات غير الصحيحة. يرجى المحاولة بعد 15 دقيقة." : "Account temporarily locked due to multiple failed attempts. Please try again after 15 minutes.",
    invalidChallenge: isArabic ? "انتهت صلاحية جلسة المصادقة. يرجى تسجيل الدخول مجددًا." : "Authentication session expired. Please sign in again.",
    invalidMfaCode: isArabic ? "رمز التحقق غير صحيح. يرجى التأكد من الرمز والمحاولة مجددًا." : "Invalid verification code. Please check your app and try again.",
    mfaAttemptsExceeded: isArabic ? "تجاوزت الحد الأقصى لمحاولات التحقق. يرجى تسجيل الدخول مجددًا." : "Maximum verification attempts exceeded. Please sign in again.",
    passwordTooShort: isArabic ? "يجب ألا تقل كلمة المرور عن 12 حرفًا." : "Password must be at least 12 characters long."
  };

  function mapAuthError(err: unknown): string {
    if (err instanceof ApiError) {
      const code = err.problemDetails?.code || ((err.problemDetails as Record<string, unknown> | undefined)?.extensions as Record<string, unknown> | undefined)?.code as string | undefined;
      switch (code) {
        case "auth.invalid_credentials":
          return t.invalidCredentials;
        case "auth.account_suspended":
          return t.accountSuspended;
        case "auth.account_locked":
          return t.accountLocked;
        case "auth.invalid_challenge":
        case "auth.expired_challenge":
          return t.invalidChallenge;
        case "auth.invalid_mfa_code":
          return t.invalidMfaCode;
        case "auth.mfa_attempts_exceeded":
          return t.mfaAttemptsExceeded;
        case "auth.password_too_short":
          return t.passwordTooShort;
        default:
          return err.problemDetails?.title || err.message || t.defaultError;
      }
    }
    return t.defaultError;
  }

  async function submitLogin(loginEmail: string, loginPassword: string) {
    setError(undefined);
    setSubmitting(true);
    authenticatedEmailRef.current = loginEmail.trim();

    try {
      const result = await loginUser(loginEmail.trim(), loginPassword);
      handleNextStep(result.state, result.challengeToken);
    } catch (err) {
      setError(mapAuthError(err));
    } finally {
      setSubmitting(false);
    }
  }

  function handleLoginSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitLogin(email, password);
  }

  function handleDevelopmentLogin(profile: DevelopmentLoginProfile) {
    if (!developmentLoginPassword) return;
    setEmail(profile.email);
    setPassword(developmentLoginPassword);
    void submitLogin(profile.email, developmentLoginPassword);
  }

  async function handleInitialPasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(undefined);

    if (newPassword !== confirmPassword) {
      setError(t.passwordsDoNotMatch);
      return;
    }

    if (newPassword.length < 12) {
      setError(t.passwordTooShort);
      return;
    }

    setSubmitting(true);
    try {
      const result = await changeInitialPassword(challengeToken, newPassword);
      handleNextStep(result.state, result.challengeToken);
    } catch (err) {
      setError(mapAuthError(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleMfaSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(undefined);
    setSubmitting(true);

    try {
      const result = await verifyMfa(challengeToken, mfaCode.trim());
      handleNextStep(result.state, result.challengeToken);
    } catch (err) {
      setError(mapAuthError(err));
    } finally {
      setSubmitting(false);
    }
  }

  function handleNextStep(nextState: AuthNextStep, token?: string | undefined) {
    if (token) {
      setChallengeToken(token);
    }

    switch (nextState) {
      case "Authenticated":
        onAuthenticated(authenticatedEmailRef.current);
        break;
      case "ChangePassword":
        setStep("initial-password");
        setError(undefined);
        break;
      case "VerifyMfa":
        setStep("mfa");
        setError(undefined);
        break;
      case "Rejected":
      default:
        setError(t.defaultError);
        break;
    }
  }

  function resetToLogin() {
    setStep("login");
    setChallengeToken("");
    setPassword("");
    setNewPassword("");
    setConfirmPassword("");
    setMfaCode("");
    setError(undefined);
  }

  return (
    <main
      className="auth-page"
      dir={direction}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100vh",
        padding: "1.5rem",
        backgroundColor: "var(--color-surface-page, #f8fafc)",
        fontFamily: "var(--font-sans, system-ui, -apple-system, sans-serif)",
        background: "radial-gradient(circle at 50% 0%, rgba(15, 118, 110, 0.08) 0%, transparent 75%), var(--color-surface-page, #f8fafc)"
      }}
    >
      <div
        className="auth-container"
        style={{
          width: "100%",
          maxWidth: "880px",
          display: "grid",
          gridTemplateColumns: "1fr 1fr",
          backgroundColor: "var(--color-surface-card, #ffffff)",
          borderRadius: "var(--radius-xl, 1rem)",
          boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.08), 0 8px 10px -6px rgba(0, 0, 0, 0.03)",
          border: "1px solid var(--color-border-default, #e2e8f0)",
          overflow: "hidden",
          minHeight: "560px"
        }}
      >
        {/* Brand Showcase Hero Column */}
        <div
          className="auth-hero-column"
          style={{
            backgroundColor: "oklch(0.2 0.04 245)",
            color: "#ffffff",
            padding: "3rem 2.5rem",
            display: "flex",
            flexDirection: "column",
            justifyContent: "space-between",
            position: "relative",
            overflow: "hidden",
            background: "linear-gradient(145deg, oklch(0.22 0.05 245) 0%, oklch(0.14 0.03 245) 100%)"
          }}
        >
          {/* Subtle geometric background glow */}
          <div
            style={{
              position: "absolute",
              top: "-20%",
              insetInlineEnd: "-20%",
              width: "300px",
              height: "300px",
              borderRadius: "50%",
              background: "radial-gradient(circle, rgba(15, 118, 110, 0.3) 0%, transparent 70%)",
              pointerEvents: "none"
            }}
          />

          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "2rem" }}>
              <div
                style={{
                  width: "40px",
                  height: "40px",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "var(--color-action-primary, #0f766e)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontWeight: 900,
                  fontSize: "1.25rem",
                  color: "#ffffff",
                  boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.2)"
                }}
              >
                T
              </div>
              <span style={{ fontSize: "1.5rem", fontWeight: 800, letterSpacing: "0.06em", color: "#ffffff" }}>
                TODIR
              </span>
            </div>

            <h2
              style={{
                margin: "0 0 1rem 0",
                fontSize: "1.625rem",
                fontWeight: 800,
                lineHeight: 1.35,
                color: "#ffffff"
              }}
            >
              {isArabic
                ? "نظام تخطيط الموارد المالي والإداري للشركات الرائدة"
                : "Enterprise Resource Planning for Modern Business"}
            </h2>

            <p
              style={{
                margin: 0,
                fontSize: "0.875rem",
                lineHeight: 1.6,
                color: "rgba(255, 255, 255, 0.75)"
              }}
            >
              {isArabic
                ? "إدارة مالية دقيقة، وعزل أمني صارم، وسرعة فائقة لدعم نمو أعمالك بثقة واحترافية."
                : "Exact financial tracking, strict multi-tenant isolation, and high performance designed for operational confidence."}
            </p>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", marginTop: "2rem" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "0.625rem", fontSize: "0.8125rem", color: "rgba(255, 255, 255, 0.85)" }}>
              <span style={{ color: "#34d399" }}>✓</span>
              <span>{isArabic ? "عزل بيانات المؤسسات وتشفير متكامل" : "Strict Tenant Isolation & Encryption"}</span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.625rem", fontSize: "0.8125rem", color: "rgba(255, 255, 255, 0.85)" }}>
              <span style={{ color: "#34d399" }}>✓</span>
              <span>{isArabic ? "توافق كامل مع متطلبات السوق العربي والفوترة" : "Arabic-First & Local Compliance"}</span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.625rem", fontSize: "0.8125rem", color: "rgba(255, 255, 255, 0.85)" }}>
              <span style={{ color: "#34d399" }}>✓</span>
              <span>{isArabic ? "مصادقة متقدمة وحماية متعددة العوامل MFA" : "Progressive MFA & Session Security"}</span>
            </div>
          </div>
        </div>

        {/* Form Column */}
        <div
          className="auth-form-column"
          style={{
            padding: "2.75rem 2.5rem",
            display: "flex",
            flexDirection: "column",
            justifyContent: "center"
          }}
        >
          <div style={{ marginBottom: "1.75rem" }}>
            <h1
              style={{
                margin: 0,
                fontSize: "1.5rem",
                fontWeight: 700,
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              {step === "login"
                ? t.loginTitle
                : step === "initial-password"
                ? t.initPasswordTitle
                : t.mfaTitle}
            </h1>
            <p
              style={{
                margin: "0.375rem 0 0 0",
                fontSize: "0.875rem",
                color: "var(--color-content-muted, #64748b)",
                lineHeight: 1.5
              }}
            >
              {step === "login"
                ? t.loginSubtitle
                : step === "initial-password"
                ? t.initPasswordSubtitle
                : t.mfaSubtitle}
            </p>
          </div>

          {/* Global Error Alert */}
          {error && (
            <div style={{ marginBottom: "1.25rem" }}>
              <Alert variant="danger">{error}</Alert>
            </div>
          )}

          {/* STEP 1: LOGIN FORM */}
          {step === "login" && (
            <form onSubmit={(e) => void handleLoginSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1.125rem" }}>
              <div>
                <label
                  htmlFor="login-email"
                  style={{
                    display: "block",
                    fontSize: "0.875rem",
                    fontWeight: 600,
                    marginBottom: "0.375rem",
                    color: "var(--color-content-primary, #0f172a)"
                  }}
                >
                  {t.emailLabel}
                </label>
                <input
                  id="login-email"
                  type="email"
                  autoComplete="username"
                  autoFocus
                  required
                  placeholder={t.emailPlaceholder}
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  style={{
                    width: "100%",
                    padding: "0.625rem 0.875rem",
                    fontSize: "0.9375rem",
                    borderRadius: "var(--radius-md, 0.375rem)",
                    border: "1px solid var(--color-border-default, #cbd5e1)",
                    backgroundColor: "var(--color-surface-subtle, #ffffff)",
                    color: "var(--color-content-primary, #0f172a)",
                    boxSizing: "border-box",
                    direction: "ltr",
                    textAlign: isArabic ? "right" : "left"
                  }}
                />
              </div>

              <PasswordField
                id="login-password"
                label={t.passwordLabel}
                isRequired
                autoComplete="current-password"
                value={password}
                onChange={setPassword}
              />

              <Button
                type="submit"
                variant="primary"
                isLoading={submitting}
                style={{ width: "100%", marginTop: "0.5rem" }}
              >
                {submitting ? t.signingIn : t.signInButton}
              </Button>

              {developmentLoginProfiles.length > 0 && (
                <fieldset
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    gap: "0.5rem",
                    margin: "0.5rem 0 0",
                    padding: "0.75rem",
                    border: "1px dashed var(--color-border-default, #cbd5e1)",
                    borderRadius: "var(--radius-md, 0.375rem)"
                  }}
                >
                  <legend
                    style={{
                      paddingInline: "0.375rem",
                      fontSize: "0.75rem",
                      fontWeight: 700,
                      color: "var(--color-content-muted, #64748b)"
                    }}
                  >
                    {t.developmentAccountsTitle}
                  </legend>
                  {developmentLoginProfiles.map((profile) => (
                    <Button
                      key={profile.email}
                      type="button"
                      variant="secondary"
                      size="compact"
                      isDisabled={submitting}
                      onPress={() => handleDevelopmentLogin(profile)}
                      style={{ width: "100%", justifyContent: "space-between" }}
                    >
                      <span>{profile.label}</span>
                      <span dir="ltr" style={{ fontSize: "0.75rem" }}>{profile.email}</span>
                    </Button>
                  ))}
                </fieldset>
              )}
            </form>
          )}

        {/* STEP 2: INITIAL PASSWORD CHANGE */}
        {step === "initial-password" && (
          <form onSubmit={(e) => void handleInitialPasswordSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            <PasswordField
              id="new-password"
              label={t.newPasswordLabel}
              isRequired
              autoFocus
              autoComplete="new-password"
              minLength={12}
              value={newPassword}
              onChange={setNewPassword}
              description={t.passwordRequirements}
            />

            <PasswordField
              id="confirm-password"
              label={t.confirmPasswordLabel}
              isRequired
              autoComplete="new-password"
              minLength={12}
              value={confirmPassword}
              onChange={setConfirmPassword}
            />

            <Button
              type="submit"
              variant="primary"
              isLoading={submitting}
              style={{ width: "100%", marginTop: "0.5rem" }}
            >
              {submitting ? t.savingPassword : t.savePasswordButton}
            </Button>
          </form>
        )}

        {/* STEP 3: MFA CODE VERIFICATION */}
        {step === "mfa" && (
          <form onSubmit={(e) => void handleMfaSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            <div>
              <label
                htmlFor="mfa-code-input"
                style={{
                  display: "block",
                  fontSize: "0.875rem",
                  fontWeight: 600,
                  marginBottom: "0.375rem",
                  color: "var(--color-content-primary, #0f172a)"
                }}
              >
                {t.mfaCodeLabel}
              </label>
              <input
                id="mfa-code-input"
                type="text"
                inputMode="numeric"
                pattern="[0-9]*"
                autoComplete="one-time-code"
                autoFocus
                required
                maxLength={8}
                placeholder={t.mfaCodePlaceholder}
                value={mfaCode}
                onChange={(e) => setMfaCode(e.target.value)}
                style={{
                  width: "100%",
                  padding: "0.75rem 1rem",
                  fontSize: "1.25rem",
                  fontWeight: 700,
                  letterSpacing: "0.25em",
                  textAlign: "center",
                  borderRadius: "var(--radius-md, 0.375rem)",
                  border: "1px solid var(--color-border-default, #cbd5e1)",
                  backgroundColor: "var(--color-surface-subtle, #ffffff)",
                  color: "var(--color-content-primary, #0f172a)",
                  boxSizing: "border-box",
                  direction: "ltr"
                }}
              />
            </div>

            <Button
              type="submit"
              variant="primary"
              isLoading={submitting}
              style={{ width: "100%", marginTop: "0.5rem" }}
            >
              {submitting ? t.verifying : t.verifyButton}
            </Button>

            <Button
              type="button"
              variant="ghost"
              onPress={resetToLogin}
              style={{ width: "100%" }}
            >
              {t.backToLogin}
            </Button>
          </form>
        )}
        </div>
      </div>
    </main>
  );
}
