import React, { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../primitives/dialog.tsx";
import { Button } from "../primitives/button.tsx";
import {
  HelpCircleIcon,
  LightbulbIcon,
  BookOpenIcon,
  CheckCircleIcon
} from "../primitives/icons.tsx";

export interface PageHelpStep {
  readonly step: number;
  readonly title: string;
  readonly description: string;
}

export interface PageHelpConcept {
  readonly term: string;
  readonly explanation: string;
}

export interface PageHelpFaq {
  readonly question: string;
  readonly answer: string;
}

export interface PageHelpContent {
  readonly title?: string | undefined;
  readonly subtitle?: string | undefined;
  readonly summary: string;
  readonly workflowSteps?: readonly PageHelpStep[] | undefined;
  readonly keyConcepts?: readonly PageHelpConcept[] | undefined;
  readonly proTips?: readonly string[] | undefined;
  readonly faqs?: readonly PageHelpFaq[] | undefined;
}

export interface PageHelpModalProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (isOpen: boolean) => void;
  readonly content: PageHelpContent;
}

export function PageHelpModal({
  isOpen,
  onOpenChange,
  content
}: PageHelpModalProps): React.JSX.Element | null {
  const { t } = useTranslation("common");

  if (!isOpen) return null;

  const modalTitle = content.title ?? t("pageHelp.defaultTitle");
  const modalSubtitle = content.subtitle ?? t("pageHelp.defaultSubtitle");

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={onOpenChange}
      size="large"
      title={
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
          <div
            style={{
              width: "36px",
              height: "36px",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "rgba(16, 42, 67, 0.08)",
              border: "1px solid rgba(16, 42, 67, 0.15)",
              color: "var(--color-brand-primary, #102a43)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              flexShrink: 0
            }}
          >
            <HelpCircleIcon size={20} />
          </div>
          <div>
            <span
              style={{
                fontSize: "1.125rem",
                fontWeight: 700,
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              {modalTitle}
            </span>
            <div
              style={{
                fontSize: "0.8125rem",
                color: "var(--color-content-muted, #64748b)",
                fontWeight: 500,
                marginTop: "0.125rem"
              }}
            >
              {modalSubtitle}
            </div>
          </div>
        </div>
      }
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <Button variant="primary" onPress={() => onOpenChange(false)}>
            {t("pageHelp.close")}
          </Button>
        </div>
      }
    >
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "1.25rem",
          padding: "0.25rem 0",
          maxHeight: "65vh",
          overflowY: "auto"
        }}
      >
        {/* 1. Purpose / Summary */}
        <div
          style={{
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderInlineStart: "4px solid var(--color-brand-primary, #102a43)",
            borderRadius: "var(--radius-md, 0.5rem)",
            padding: "1rem 1.25rem"
          }}
        >
          <div
            style={{
              fontSize: "0.875rem",
              fontWeight: 700,
              color: "var(--color-content-primary, #0f172a)",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem",
              marginBottom: "0.375rem"
            }}
          >
            <BookOpenIcon size={16} />
            <span>{t("pageHelp.purposeTitle")}</span>
          </div>
          <p
            style={{
              margin: 0,
              fontSize: "0.875rem",
              color: "var(--color-content-secondary, #334155)",
              lineHeight: 1.6
            }}
          >
            {content.summary}
          </p>
        </div>

        {/* 2. Step-by-Step Workflow */}
        {content.workflowSteps && content.workflowSteps.length > 0 && (
          <div>
            <h4
              style={{
                margin: "0 0 0.75rem 0",
                fontSize: "0.9375rem",
                fontWeight: 700,
                color: "var(--color-content-primary, #0f172a)",
                display: "flex",
                alignItems: "center",
                gap: "0.5rem"
              }}
            >
              <CheckCircleIcon size={16} />
              <span>{t("pageHelp.workflowTitle")}</span>
            </h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}>
              {content.workflowSteps.map((step) => (
                <div
                  key={step.step}
                  style={{
                    display: "flex",
                    alignItems: "flex-start",
                    gap: "0.75rem",
                    backgroundColor: "var(--color-surface-card, #ffffff)",
                    border: "1px solid var(--color-border-subtle, #e2e8f0)",
                    borderRadius: "var(--radius-md, 0.5rem)",
                    padding: "0.75rem 1rem",
                    boxShadow: "var(--shadow-xs, 0 1px 2px 0 rgba(15, 23, 42, 0.05))"
                  }}
                >
                  <span
                    style={{
                      width: "24px",
                      height: "24px",
                      borderRadius: "50%",
                      backgroundColor: "var(--color-brand-primary, #102a43)",
                      color: "#ffffff",
                      fontSize: "0.75rem",
                      fontWeight: 700,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      flexShrink: 0
                    }}
                  >
                    <bdi>{step.step}</bdi>
                  </span>
                  <div style={{ flex: 1 }}>
                    <div
                      style={{
                        fontSize: "0.875rem",
                        fontWeight: 700,
                        color: "var(--color-content-primary, #0f172a)",
                        marginBottom: "0.125rem"
                      }}
                    >
                      {step.title}
                    </div>
                    <div
                      style={{
                        fontSize: "0.8125rem",
                        color: "var(--color-content-secondary, #475569)",
                        lineHeight: 1.5
                      }}
                    >
                      {step.description}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 3. Key Concepts */}
        {content.keyConcepts && content.keyConcepts.length > 0 && (
          <div>
            <h4
              style={{
                margin: "0 0 0.75rem 0",
                fontSize: "0.9375rem",
                fontWeight: 700,
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              {t("pageHelp.conceptsTitle")}
            </h4>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
                gap: "0.625rem"
              }}
            >
              {content.keyConcepts.map((item, idx) => (
                <div
                  key={idx}
                  style={{
                    backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                    border: "1px solid var(--color-border-subtle, #e2e8f0)",
                    borderRadius: "var(--radius-md, 0.5rem)",
                    padding: "0.75rem 1rem"
                  }}
                >
                  <div
                    style={{
                      fontSize: "0.8125rem",
                      fontWeight: 700,
                      color: "var(--color-brand-primary, #102a43)",
                      marginBottom: "0.25rem"
                    }}
                  >
                    {item.term}
                  </div>
                  <div
                    style={{
                      fontSize: "0.8125rem",
                      color: "var(--color-content-secondary, #475569)",
                      lineHeight: 1.5
                    }}
                  >
                    {item.explanation}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 4. Pro Tips */}
        {content.proTips && content.proTips.length > 0 && (
          <div
            style={{
              backgroundColor: "rgba(217, 119, 6, 0.05)",
              border: "1px solid rgba(217, 119, 6, 0.25)",
              borderRadius: "var(--radius-md, 0.5rem)",
              padding: "0.875rem 1rem"
            }}
          >
            <div
              style={{
                fontSize: "0.875rem",
                fontWeight: 700,
                color: "var(--color-status-warning, #b45309)",
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                marginBottom: "0.5rem"
              }}
            >
              <LightbulbIcon size={16} />
              <span>{t("pageHelp.proTipsTitle")}</span>
            </div>
            <ul
              style={{
                margin: 0,
                paddingInlineStart: "1.25rem",
                fontSize: "0.8125rem",
                color: "var(--color-content-secondary, #334155)",
                lineHeight: 1.6,
                display: "flex",
                flexDirection: "column",
                gap: "0.25rem"
              }}
            >
              {content.proTips.map((tip, idx) => (
                <li key={idx}>{tip}</li>
              ))}
            </ul>
          </div>
        )}

        {/* 5. FAQs */}
        {content.faqs && content.faqs.length > 0 && (
          <div>
            <h4
              style={{
                margin: "0 0 0.75rem 0",
                fontSize: "0.9375rem",
                fontWeight: 700,
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              {t("pageHelp.faqsTitle")}
            </h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              {content.faqs.map((faq, idx) => (
                <div
                  key={idx}
                  style={{
                    backgroundColor: "var(--color-surface-card, #ffffff)",
                    border: "1px solid var(--color-border-subtle, #e2e8f0)",
                    borderRadius: "var(--radius-md, 0.5rem)",
                    padding: "0.75rem 1rem"
                  }}
                >
                  <div
                    style={{
                      fontSize: "0.8125rem",
                      fontWeight: 700,
                      color: "var(--color-content-primary, #0f172a)",
                      marginBottom: "0.25rem"
                    }}
                  >
                    {faq.question}
                  </div>
                  <div
                    style={{
                      fontSize: "0.8125rem",
                      color: "var(--color-content-secondary, #475569)",
                      lineHeight: 1.5
                    }}
                  >
                    {faq.answer}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}

export interface PageHelpButtonProps {
  readonly content: PageHelpContent;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function PageHelpButton({
  content,
  className,
  style
}: PageHelpButtonProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const [isOpen, setIsOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        aria-label={t("pageHelp.triggerAriaLabel")}
        title={t("pageHelp.triggerTooltip")}
        className={className}
        style={{
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "0.375rem",
          minWidth: "32px",
          height: "32px",
          padding: "0 0.5rem",
          borderRadius: "var(--radius-md, 0.375rem)",
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          boxShadow: "var(--shadow-xs, 0 1px 2px 0 rgba(15, 23, 42, 0.05))",
          color: "var(--color-brand-primary, #102a43)",
          fontSize: "0.8125rem",
          fontWeight: 700,
          cursor: "pointer",
          outline: "none",
          transition: "all 150ms ease",
          ...style
        }}
        onMouseEnter={(e) => {
          e.currentTarget.style.backgroundColor = "var(--color-surface-subtle, #f8fafc)";
          e.currentTarget.style.borderColor = "var(--color-brand-primary, #102a43)";
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.backgroundColor = "var(--color-surface-card, #ffffff)";
          e.currentTarget.style.borderColor = "var(--color-border-default, #cbd5e1)";
        }}
      >
        <HelpCircleIcon size={16} />
        <span style={{ fontSize: "0.75rem", fontWeight: 700 }}>?</span>
      </button>

      <PageHelpModal
        isOpen={isOpen}
        onOpenChange={setIsOpen}
        content={content}
      />
    </>
  );
}
