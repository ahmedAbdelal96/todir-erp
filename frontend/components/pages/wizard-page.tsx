import React, { type ReactNode } from "react";
import { Button } from "../primitives/button.tsx";
import { CheckIcon } from "../primitives/icons.tsx";
import clsx from "clsx";

export interface WizardStep {
  readonly id: string;
  readonly title: string;
  readonly description?: string | undefined;
}

export interface WizardPageProps {
  readonly title: string;
  readonly steps: readonly WizardStep[];
  readonly currentStepIndex: number;
  readonly onStepChange?: ((stepIndex: number) => void) | undefined;
  readonly onNext?: (() => void) | undefined;
  readonly onBack?: (() => void) | undefined;
  readonly onComplete?: (() => void) | undefined;
  readonly nextLabel?: string | undefined;
  readonly backLabel?: string | undefined;
  readonly completeLabel?: string | undefined;
  readonly isSubmitting?: boolean | undefined;
  readonly isNextDisabled?: boolean | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function WizardPage({
  title,
  steps,
  currentStepIndex,
  onStepChange,
  onNext,
  onBack,
  onComplete,
  nextLabel = "التالي",
  backLabel = "السابق",
  completeLabel = "إنهاء / حفظ",
  isSubmitting = false,
  isNextDisabled = false,
  children,
  className
}: WizardPageProps): React.JSX.Element {
  const isFirstStep = currentStepIndex === 0;
  const isLastStep = currentStepIndex === steps.length - 1;

  return (
    <div
      className={clsx("ui-wizard-page", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.75rem",
        padding: "1.5rem",
        maxWidth: "960px",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      <header style={{ textAlign: "center" }}>
        <h1
          style={{
            margin: 0,
            fontSize: "1.625rem",
            fontWeight: 800,
            color: "var(--color-content-primary)",
            fontFamily: "var(--font-sans)",
            letterSpacing: "-0.02em"
          }}
        >
          {title}
        </h1>
      </header>

      {/* Modern Stepper Indicators */}
      <nav aria-label="خطوات المعالج" style={{ padding: "0.5rem 0" }}>
        <ol
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            listStyle: "none",
            padding: 0,
            margin: 0,
            position: "relative"
          }}
        >
          {steps.map((step, idx) => {
            const isCurrent = idx === currentStepIndex;
            const isCompleted = idx < currentStepIndex;
            return (
              <li
                key={step.id}
                style={{
                  flex: 1,
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  position: "relative",
                  zIndex: 1
                }}
              >
                {/* Connector Line */}
                {idx > 0 && (
                  <div
                    style={{
                      position: "absolute",
                      top: "1.125rem",
                      insetInlineEnd: "50%",
                      width: "100%",
                      height: "2px",
                      backgroundColor: isCompleted || isCurrent
                        ? "var(--color-action-primary, #2563eb)"
                        : "var(--color-border-default, #e2e8f0)",
                      zIndex: -1,
                      transition: "background-color 0.2s ease"
                    }}
                  />
                )}

                <div
                  style={{
                    width: "2.25rem",
                    height: "2.25rem",
                    borderRadius: "50%",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: "0.875rem",
                    fontWeight: 700,
                    backgroundColor: isCurrent
                      ? "var(--color-action-primary, #2563eb)"
                      : isCompleted
                      ? "var(--color-status-success, #16a34a)"
                      : "var(--color-surface-card, #ffffff)",
                    color: isCurrent || isCompleted
                      ? "#ffffff"
                      : "var(--color-content-muted, #64748b)",
                    border: `2px solid ${
                      isCurrent
                        ? "var(--color-action-primary, #2563eb)"
                        : isCompleted
                        ? "var(--color-status-success, #16a34a)"
                        : "var(--color-border-default, #cbd5e1)"
                    }`,
                    boxShadow: isCurrent ? "0 0 0 4px var(--color-brand-primary-subtle, rgba(37, 99, 235, 0.15))" : "none",
                    transition: "all 0.2s ease"
                  }}
                >
                  {isCompleted ? <CheckIcon size={16} /> : idx + 1}
                </div>
                <span
                  style={{
                    fontSize: "0.8125rem",
                    marginTop: "0.5rem",
                    fontWeight: isCurrent ? 700 : 500,
                    color: isCurrent
                      ? "var(--color-content-primary, #0f172a)"
                      : isCompleted
                      ? "var(--color-content-secondary, #334155)"
                      : "var(--color-content-muted, #94a3b8)",
                    textAlign: "center"
                  }}
                >
                  {step.title}
                </span>
                {step.description && (
                  <span
                    style={{
                      fontSize: "0.6875rem",
                      color: "var(--color-content-muted, #94a3b8)",
                      textAlign: "center",
                      marginTop: "0.125rem",
                      display: "none"
                    }}
                  >
                    {step.description}
                  </span>
                )}
              </li>
            );
          })}
        </ol>
      </nav>

      {/* Main Step Content Card */}
      <main
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-xl, 0.75rem)",
          padding: "1.75rem",
          minHeight: "320px",
          boxShadow: "var(--shadow-sm, 0 1px 3px rgba(0, 0, 0, 0.05))"
        }}
      >
        {children}
      </main>

      {/* Navigation Footer */}
      <footer
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          paddingTop: "0.5rem"
        }}
      >
        <Button
          variant="secondary"
          onPress={onBack}
          isDisabled={isFirstStep || isSubmitting}
        >
          {backLabel}
        </Button>

        {isLastStep ? (
          <Button
            variant="primary"
            onPress={onComplete}
            isLoading={isSubmitting}
            isDisabled={isNextDisabled}
          >
            {completeLabel}
          </Button>
        ) : (
          <Button
            variant="primary"
            onPress={onNext}
            isDisabled={isNextDisabled}
            isLoading={isSubmitting}
          >
            {nextLabel}
          </Button>
        )}
      </footer>
    </div>
  );
}
