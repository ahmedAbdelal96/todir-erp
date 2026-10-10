import React from "react";
import { TextField } from "../primitives/text-field.tsx";

export interface LocalDateFieldProps {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly value?: string;
  readonly defaultValue?: string;
  readonly onChange?: (value: string) => void;
  readonly size?: "comfortable" | "compact";
  readonly isReadOnly?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly isRequired?: boolean;
  readonly minDate?: string;
  readonly maxDate?: string;
  readonly className?: string;
  readonly name?: string;
}

export function LocalDateField({
  label,
  description,
  errorMessage,
  value,
  defaultValue,
  onChange,
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  isRequired = false,
  className,
  name
}: LocalDateFieldProps): React.JSX.Element {
  return (
    <div className={className} style={{ width: "100%" }}>
      <TextField
        type="date"
        label={label}
        description={description}
        errorMessage={errorMessage}
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
