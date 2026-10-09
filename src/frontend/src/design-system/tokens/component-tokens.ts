/**
 * Level 3: Component Tokens
 * Component-specific tokens derived from semantic tokens and density metrics.
 */

export interface ButtonComponentTokens {
  readonly primaryBackground: string;
  readonly primaryBackgroundHover: string;
  readonly primaryText: string;
  readonly secondaryBackground: string;
  readonly secondaryBorder: string;
  readonly secondaryText: string;
  readonly dangerBackground: string;
  readonly dangerText: string;
  readonly heightComfortable: string;
  readonly heightCompact: string;
  readonly paddingInlineComfortable: string;
  readonly paddingInlineCompact: string;
}

export interface InputComponentTokens {
  readonly background: string;
  readonly border: string;
  readonly borderFocus: string;
  readonly text: string;
  readonly placeholder: string;
  readonly heightComfortable: string;
  readonly heightCompact: string;
  readonly paddingInline: string;
}

export interface DataGridComponentTokens {
  readonly headerBackground: string;
  readonly headerText: string;
  readonly headerBorder: string;
  readonly rowHoverBackground: string;
  readonly rowSelectedBackground: string;
  readonly rowBorder: string;
  readonly rowHeightComfortable: string;
  readonly rowHeightCompact: string;
  readonly cellPaddingInline: string;
}

export interface DialogComponentTokens {
  readonly background: string;
  readonly border: string;
  readonly shadow: string;
  readonly backdropBackground: string;
}

export interface SidebarComponentTokens {
  readonly background: string;
  readonly border: string;
  readonly itemHoverBackground: string;
  readonly itemActiveBackground: string;
  readonly itemActiveText: string;
}

export interface ComponentTokens {
  readonly button: ButtonComponentTokens;
  readonly input: InputComponentTokens;
  readonly grid: DataGridComponentTokens;
  readonly dialog: DialogComponentTokens;
  readonly sidebar: SidebarComponentTokens;
}
