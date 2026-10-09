import React, { type ReactNode } from "react";
import { Button } from "../primitives/button.tsx";
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
        gap: "1.5rem",
        padding: "1.5rem",
        maxWidth: "900px",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      <header style={{ textAlign: "center" }}>
        <h1
          style={{
            margin: 0,
            fontSize: "1.5rem",
            fontWeight: 700,
            color: "var(--color-content-primary)",
            fontFamily: "var(--font-sans)"
          }}
        >
          {title}
        </h1>
      </header>

      {/* Step Indicators */}
      <nav aria-label="Progress" style={{ padding: "0.5rem 0" }}>
        <ol
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            listStyle: "none",
            padding: 0,
            margin: 0,
            gap: "0.5rem"
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
                  position: "relative"
                }}
              >
                <div
                  style={{
                    width: "2rem",
                    height: "2rem",
                    borderRadius: "50%",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: "0.875rem",
                    fontWeight: 600,
                    backgroundColor: isCurrent || isCompleted
                      ? "var(--color-action-primary)"
                      : "var(--color-surface-subtle)",
                    color: isCurrent || isCompleted
                      ? "var(--color-action-primary-content)"
                      : "var(--color-content-muted)",
                    border: `2px solid ${isCurrent ? "var(--color-action-primary)" : "transparent"}`
                  }}
                >
                  {isCompleted ? "✓" : idx + 1}
                </div>
                <span
                  style={{
                    fontSize: "0.75rem",
                    marginTop: "0.375rem",
                    fontWeight: isCurrent ? 600 : 400,
                    color: isCurrent ? "var(--color-content-primary)" : "var(--color-content-muted)",
                    textAlign: "center"
                  }}
                >
                  {step.title}
                </span>
              </li>
            );
          })}
        </ol>
      </nav>

      {/* Main Step Content */}
      <main
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-lg, 0.5rem)",
          padding: "1.5rem",
          minHeight: "300px"
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
          paddingTop: "1rem"
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
