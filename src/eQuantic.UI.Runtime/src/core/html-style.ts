/**
 * Inline CSS styles with type-safe properties
 */
export class HtmlStyle {
  // Layout
  display?: string;
  position?: string;
  top?: string;
  right?: string;
  bottom?: string;
  left?: string;
  zIndex?: string;

  // Flexbox
  flexDirection?: string;
  flexWrap?: string;
  justifyContent?: string;
  alignItems?: string;
  alignContent?: string;
  gap?: string;
  flex?: string;
  flexGrow?: string;
  flexShrink?: string;

  // Grid
  gridTemplateColumns?: string;
  gridTemplateRows?: string;
  gridColumn?: string;
  gridRow?: string;
  gridAutoFlow?: string;
  justifyItems?: string;

  // Sizing
  width?: string;
  height?: string;
  minWidth?: string;
  minHeight?: string;
  maxWidth?: string;
  maxHeight?: string;

  // Spacing
  margin?: string;
  marginTop?: string;
  marginRight?: string;
  marginBottom?: string;
  marginLeft?: string;
  padding?: string;
  paddingTop?: string;
  paddingRight?: string;
  paddingBottom?: string;
  paddingLeft?: string;

  // Background
  background?: string;
  backgroundColor?: string;
  backgroundImage?: string;

  // Border
  border?: string;
  borderWidth?: string;
  borderStyle?: string;
  borderColor?: string;
  borderRadius?: string;

  // Typography
  color?: string;
  fontFamily?: string;
  fontSize?: string;
  fontWeight?: string;
  fontStyle?: string;
  lineHeight?: string;
  textAlign?: string;
  textDecoration?: string;
  textTransform?: string;
  letterSpacing?: string;

  // Effects
  boxShadow?: string;
  opacity?: string;
  cursor?: string;
  overflow?: string;
  overflowX?: string;
  overflowY?: string;
  transition?: string;
  transform?: string;

  constructor(init?: Partial<HtmlStyle>) {
    if (init) {
      Object.assign(this, init);
    }
  }

  /**
   * Convert to CSS string for inline styles
   */
  toCssString(): string {
    const properties: string[] = [];
    const cssMap: Record<string, string> = {
      // Convert camelCase to kebab-case
      display: 'display',
      position: 'position',
      top: 'top',
      right: 'right',
      bottom: 'bottom',
      left: 'left',
      zIndex: 'z-index',
      flexDirection: 'flex-direction',
      flexWrap: 'flex-wrap',
      justifyContent: 'justify-content',
      alignItems: 'align-items',
      alignContent: 'align-content',
      gap: 'gap',
      flex: 'flex',
      flexGrow: 'flex-grow',
      flexShrink: 'flex-shrink',
      gridTemplateColumns: 'grid-template-columns',
      gridTemplateRows: 'grid-template-rows',
      gridColumn: 'grid-column',
      gridRow: 'grid-row',
      gridAutoFlow: 'grid-auto-flow',
      justifyItems: 'justify-items',
      width: 'width',
      height: 'height',
      minWidth: 'min-width',
      minHeight: 'min-height',
      maxWidth: 'max-width',
      maxHeight: 'max-height',
      margin: 'margin',
      marginTop: 'margin-top',
      marginRight: 'margin-right',
      marginBottom: 'margin-bottom',
      marginLeft: 'margin-left',
      padding: 'padding',
      paddingTop: 'padding-top',
      paddingRight: 'padding-right',
      paddingBottom: 'padding-bottom',
      paddingLeft: 'padding-left',
      background: 'background',
      backgroundColor: 'background-color',
      backgroundImage: 'background-image',
      border: 'border',
      borderWidth: 'border-width',
      borderStyle: 'border-style',
      borderColor: 'border-color',
      borderRadius: 'border-radius',
      color: 'color',
      fontFamily: 'font-family',
      fontSize: 'font-size',
      fontWeight: 'font-weight',
      fontStyle: 'font-style',
      lineHeight: 'line-height',
      textAlign: 'text-align',
      textDecoration: 'text-decoration',
      textTransform: 'text-transform',
      letterSpacing: 'letter-spacing',
      boxShadow: 'box-shadow',
      opacity: 'opacity',
      cursor: 'cursor',
      overflow: 'overflow',
      overflowX: 'overflow-x',
      overflowY: 'overflow-y',
      transition: 'transition',
      transform: 'transform',
    };

    for (const [key, cssName] of Object.entries(cssMap)) {
      const value = (this as any)[key];
      if (value !== undefined && value !== null) {
        properties.push(`${cssName}: ${value}`);
      }
    }

    return properties.join('; ');
  }
}
