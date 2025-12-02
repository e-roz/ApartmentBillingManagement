🍵 MATCHA DESIGN SYSTEM DOCUMENTATION

For Apartment Billing System — ASP.NET Core / Razor Pages

1. BRAND FOUNDATION
1.1 Brand Personality

Calm — stress-free look for financial tasks

Earthy & Natural — inspired by matcha, tea, wood

Professional — clean enough for landlords/landladies

Simple & Minimalistic — Japanese-style clarity

Fresh — refreshing hues and soft gradients

1.2 Brand Keywords

Matcha • Warm • Organic • Modern • Minimal • Confident • Clean

2. COLOR SYSTEM (MATCHA PALETTE)

Matcha palette should feel natural, warm, aromatic, not neon or loud.

2.1 Primary Colors (Greens & Earth Tones)
Name	Hex	RGB	Usage
Matcha Deep Green	#4A6F47	rgb(74, 111, 71)	Primary buttons, headings, accents
Matcha Soft Green	#A8C3A0	rgb(168, 195, 160)	Section backgrounds, table rows
Ceremonial Matcha	#7DA26C	rgb(125, 162, 108)	Hover states, cards, highlights
Fresh Cream White	#F9F8F3	rgb(249, 248, 243)	Main background
2.2 Secondary Palette (Natural Accents)
Name	Hex	RGB	Usage
Roasted Brown	#8B6F47	rgb(139, 111, 71)	Divider lines, subtle text
Golden Whisk	#D7A85A	rgb(215, 168, 90)	Notifications, subtle highlights
Azuki Red	#B35A5A	rgb(179, 90, 90)	Errors, overdue indicators
2.3 Neutral Palette
Name	Hex	RGB	Usage
Ink Black	#2C2C2C	rgb(44, 44, 44)	Main text
Leaf Gray	#7B7B7B	rgb(123, 123, 123)	Secondary text
Tea Mist	#E7E5D8	rgb(231, 229, 216)	Borders, dividers
2.4 Color Rules
✔ Do:

Use Matcha Deep Green for anchors (titles, CTAs).

Use Soft Green for calming backgrounds.

Maintain earthy, warm tones throughout.

Use reds sparingly — only for overdue payments.

❌ Don’t:

Mix matcha greens with neon greens.

Combine too many shades of green in one component.

Use Golden Whisk for buttons (accent only).

3. TYPOGRAPHY SYSTEM

Matcha style = soft, modern, calm.

3.1 Font Families
Role	Font	Backup
Headings	Cormorant Garamond	Georgia, serif
Body	Inter / Nunito	Arial, sans-serif
Numeric/Code	JetBrains Mono	Consolas, monospace

Reasoning:

Cormorant → elegant, Japanese-inspired editorial feel

Inter/Nunito → clean and readable

JetBrains Mono → clarity for financial amounts

3.2 Type Scale (1.25 modular)
Style	Size	Weight	Family
H1	36px	700	Cormorant
H2	30px	700	Cormorant
H3	24px	600	Cormorant
Body L	18px	400	Inter/Nunito
Body R	16px	400	Inter/Nunito
Body S	14px	400	Inter/Nunito
Numeric	16px	500	JetBrains Mono
3.3 Typography Rules
✔ Do:

Use serif headings for sophistication.

Keep body text relaxed with good spacing.

Use monospace font for rent, balances, invoice numbers.

❌ Don’t:

Use serif fonts for paragraph text.

Tighten line height under 1.5.

4. GRID & SPACING SYSTEM
4.1 Grid Layout

12 columns

Gutter: 24px

Max width: 1200–1240px

Page padding:

Desktop: 32px

Mobile: 16px

4.2 Spacing Scale (4pt system)
Token	Value
4	4px
8	8px
12	12px
16	16px
20	20px
24	24px
32	32px
40	40px
4.3 Layout Rules
✔ Do:

Use wide breathing room (matcha = calm).

Keep layouts airy with soft spacing.

❌ Don’t:

Crowd components tightly.

Use center alignment for long text.

5. UI COMPONENT GUIDELINES
5.1 Buttons
Primary Button (Matcha Action)

Background: Matcha Deep Green (#4A6F47)

Text: white

Radius: 8px (matcha = soft corners)

Padding: 12px 20px

Hover: lighter green (#7DA26C)

Animation: 150ms ease-out

Secondary Button

Border: 1px solid #4A6F47

Text: Matcha Deep Green

Hover: soft green background

Danger Button

Background: Azuki Red

Hover: deeper red

Button Do’s

✔ Use primary for main actions (Generate Bill, Confirm Payment)
✔ Keep corners soft
✔ Use clear padding and spacing

Button Don’ts

❌ Don’t use many button colors on one page
❌ Don’t use bright neon greens

5.2 Cards (Matcha Aesthetic)

Background: Fresh Cream White

Border radius: 16px

Shadow: soft & diffused (tea-house ambiance)

Accent top border: Golden Whisk (#D7A85A)

Optional background: subtle matcha powder gradient

Card Do’s

✔ Use for summary metrics
✔ Use consistent padding
✔ Use gentle tones

Don’ts

❌ Don’t use harsh shadows
❌ Don’t mix card styles

5.3 Tables (Billing-Centric)

Header: Matcha Deep Green background, white text

Rows: alternating Soft Green (#A8C3A0) and white

Numbers right-aligned

Financial values = monospace font

Divider lines: Tea Mist (#E7E5D8)

5.4 Navigation
Sidebar

Background: Deep Matcha Green

Text: white

Icons: thin, minimalist, Japanese-inspired

Active item: soft-green highlight bar

Top Nav

Background: Fresh Cream White

Border bottom: Tea Mist

6. INTERACTIONS & MOTION
Motion Principles

Matcha = calm
→ Smooth, slow, soft animations
Duration guide:

Fast: 120ms

Normal: 200ms

Calm: 300ms

Recommended Delightful Interactions
1. Matcha Drop Loading Animation

A soft green drop forms → ripples → fades.

2. Gentle Card Hover

Card slightly lifts
Shadow becomes warm beige
Matcha powder texture lightly reveals (5% opacity)

3. Payment Progress “Tea Whisk Spinner”

Progress indicator rotates softly like a chasen whisk.

7. ICONOGRAPHY
Icon Style:

Thin-line

Modern

Rounded corners

Minimal, Japanese-inspired forms

Color:

Deep Matcha Green

White (on dark backgrounds)

Use cases:

Invoice = leaf icon

Payments = yen-inspired circle motif

Units = house outline

Settings = bamboo gear icon

8. SPECIAL MATCHA VISUAL ELEMENTS
8.1 Matcha Gradients

Use sparingly.

Example:
Soft Green → Ceremonial Matcha
linear-gradient(135deg, #A8C3A0, #7DA26C)

8.2 Japanese Paper Texture (Optional)

Use with very low opacity (3–5%) on backgrounds.

8.3 Soft Matcha Dust Decorative Corners

Used in dashboards only.
Never in tables.

9. ACCESSIBILITY

Contrast ratio minimum: 4.5:1

Focus ring: 2px solid Golden Whisk

Buttons must always have text labels, not icon-only

10. DO & DON'T SUMMARY
✔ MATCHA DO’S

Calm, soft, warm tones

Use greens + creams + natural browns

Keep motion slow and relaxing

Use generous spacing

Make financial data clean and readable

❌ MATCHA DON’TS

No neon greens

No harsh shadows

No loud animations

No clutter