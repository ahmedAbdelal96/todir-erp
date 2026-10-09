import React, { type ReactElement } from "react";
import {
  Controller,
  type Control,
  type FieldPath,
  type FieldValues,
  type ControllerRenderProps,
  type ControllerFieldState
} from "react-hook-form";

export interface FormFieldProps<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>
> {
  readonly name: TName;
  readonly control: Control<TFieldValues>;
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isReadOnly?: boolean | undefined;
  readonly render: (props: {
    field: ControllerRenderProps<TFieldValues, TName>;
    fieldState: ControllerFieldState;
    label?: string | undefined;
    description?: string | undefined;
    errorMessage?: string | undefined;
    isLocked?: boolean | undefined;
    lockedReason?: string | undefined;
    isRequired?: boolean | undefined;
    isReadOnly?: boolean | undefined;
  }) => ReactElement;
}

export function FormField<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>
>({
  name,
  control,
  label,
  description,
  isLocked,
  lockedReason,
  isRequired,
  isReadOnly,
  render
}: FormFieldProps<TFieldValues, TName>): React.JSX.Element {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) =>
        render({
          field,
          fieldState,
          label,
          description,
          errorMessage: fieldState.error?.message,
          isLocked,
          lockedReason,
          isRequired,
          isReadOnly
        })
      }
    />
  );
}
