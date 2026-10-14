import React, { useRef, useState, type ChangeEvent, type DragEvent } from "react";
import { Button } from "./button.tsx";
import { FolderIcon, FileTextIcon, CloseIcon } from "./icons.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";

export interface FileInputProps {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly accept?: string | undefined;
  readonly maxSizeBytes?: number | undefined; // e.g. 10 * 1024 * 1024 (10MB)
  readonly multiple?: boolean | undefined;
  readonly files?: readonly File[] | undefined;
  readonly onFilesChange: (files: File[]) => void;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly errorMessage?: string | undefined;
  readonly className?: string | undefined;
}

export function FileInput({
  label,
  description,
  accept,
  maxSizeBytes,
  multiple = false,
  files = [],
  onFilesChange,
  isRequired = false,
  isDisabled = false,
  errorMessage,
  className
}: FileInputProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes.toLocaleString(currentIntlLocale())} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toLocaleString(currentIntlLocale(), { maximumFractionDigits: 1 })} KB`;
    return `${(bytes / (1024 * 1024)).toLocaleString(currentIntlLocale(), { maximumFractionDigits: 2 })} MB`;
  };

  const validateAndAddFiles = (newFiles: FileList | null) => {
    if (!newFiles || newFiles.length === 0) return;
    setLocalError(null);

    const validFiles: File[] = [];
    for (let i = 0; i < newFiles.length; i++) {
      const file = newFiles[i];
      if (!file) continue;

      if (maxSizeBytes && file.size > maxSizeBytes) {
        setLocalError(t("components.fileTooLarge", { name: file.name, size: formatFileSize(maxSizeBytes) }));
        return;
      }
      validFiles.push(file);
    }

    if (multiple) {
      onFilesChange([...files, ...validFiles]);
    } else {
      onFilesChange(validFiles.slice(0, 1));
    }
  };

  const handleInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    validateAndAddFiles(e.target.files);
    if (inputRef.current) inputRef.current.value = "";
  };

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragOver(false);
    if (isDisabled) return;
    validateAndAddFiles(e.dataTransfer.files);
  };

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (!isDisabled) setIsDragOver(true);
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleRemoveFile = (index: number) => {
    if (isDisabled) return;
    const next = files.filter((_, i) => i !== index);
    onFilesChange(next);
  };

  const effectiveError = errorMessage || localError;

  return (
    <div className={clsx("ui-file-input", className)} style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      {label && (
        <label style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
          {label} {isRequired && <span style={{ color: "var(--color-status-danger)" }}>*</span>}
        </label>
      )}

      {description && (
        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
          {description}
        </p>
      )}

      <input
        ref={inputRef}
        type="file"
        accept={accept}
        multiple={multiple}
        disabled={isDisabled}
        onChange={handleInputChange}
        style={{ display: "none" }}
        aria-label={label ?? t("components.fileChoose")}
      />

      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => !isDisabled && inputRef.current?.click()}
        style={{
          border: `2px dashed ${isDragOver ? "var(--color-action-primary, #0f766e)" : effectiveError ? "var(--color-status-danger)" : "var(--color-border-default, #cbd5e1)"}`,
          backgroundColor: isDragOver ? "var(--color-surface-subtle, #f0fdf4)" : "var(--color-surface-subtle, #f8fafc)",
          borderRadius: "var(--radius-md, 0.375rem)",
          padding: "1.75rem 1.5rem",
          textAlign: "center",
          cursor: isDisabled ? "not-allowed" : "pointer",
          opacity: isDisabled ? 0.6 : 1,
          transition: "border-color 0.15s ease, background-color 0.15s ease"
        }}
        role="button"
        tabIndex={isDisabled ? -1 : 0}
        onKeyDown={(e) => {
          if (!isDisabled && (e.key === "Enter" || e.key === " ")) {
            e.preventDefault();
            inputRef.current?.click();
          }
        }}
      >
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "0.5rem", color: "var(--color-action-primary, #0f766e)" }}>
          <FolderIcon size={32} />
        </div>
        <p style={{ margin: "0 0 0.25rem 0", fontWeight: 600, fontSize: "0.875rem", color: "var(--color-content-primary, #0f172a)" }}>
          {t("components.fileBrowse")}
        </p>
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
          {accept ? t("components.supportedFormats", { formats: accept }) : t("components.allFileTypes")}
          {maxSizeBytes && ` (${t("components.maxFileSize", { size: formatFileSize(maxSizeBytes) })})`}
        </span>
      </div>

      {effectiveError && (
        <div style={{ fontSize: "0.8125rem", color: "var(--color-status-danger)" }} role="alert">
          {effectiveError}
        </div>
      )}

      {files.length > 0 && (
        <ul style={{ listStyle: "none", margin: 0, padding: 0, display: "flex", flexDirection: "column", gap: "0.375rem" }}>
          {files.map((file, index) => (
            <li
              key={`${file.name}-${index}`}
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-sm, 0.25rem)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                border: "1px solid var(--color-border-default, #e2e8f0)",
                fontSize: "0.8125rem"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", overflow: "hidden" }}>
                <span style={{ color: "var(--color-action-primary, #0f766e)", display: "inline-flex" }}>
                  <FileTextIcon size={16} />
                </span>
                <span style={{ fontWeight: 500, textOverflow: "ellipsis", overflow: "hidden", whiteSpace: "nowrap" }}>
                  {file.name}
                </span>
                <span style={{ color: "var(--color-content-muted, #64748b)", fontSize: "0.75rem" }}>
                  ({formatFileSize(file.size)})
                </span>
              </div>
              <Button
                variant="ghost"
                size="compact"
                isDisabled={isDisabled}
                onPress={() => handleRemoveFile(index)}
                aria-label={t("components.deleteFile", { name: file.name })}
              >
                <CloseIcon size={14} />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
