import React from "react";
import { TextField } from "../primitives/text-field.tsx";

export interface EmailFieldProps {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly errorMessage?: string | undefined;
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  readonly onChange?: ((value: string) => void) | undefined;
  readonly placeholder?: string | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly isReadOnly?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly isRequired?: boolean | undefined;
  readonly className?: string | undefined;
  readonly name?: string | undefined;
}

export function EmailField({
  label,
  description,
  errorMessage,
  value,
  defaultValue,
  onChange,
  placeholder = "name@company.com",
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  isRequired = false,
  className,
  name
}: EmailFieldProps): React.JSX.Element {
  return (
    <div className={className} style={{ width: "100%" }}>
      <TextField
        type="email"
        dir="ltr"
        label={label}
        description={description}
        errorMessage={errorMessage}
        placeholder={placeholder}
        size={size}
        isReadOnly={isReadOnly}
        isLocked={isLocked}
        lockedReason={lockedReason}
        isRequired={isRequired}
        name={name}
        value={value}
        defaultValue={defaultValue}
        onChange={onChange}
      />
    </div>
  );
}
