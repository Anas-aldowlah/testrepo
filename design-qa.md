# Home Product Slider Visual QA

- Source visual truth: `C:/Users/WinDows/AppData/Local/Temp/codex-clipboard-efc8ea7f-ff74-4569-bf07-9a578c822b71.png`
- Implementation screenshot: unavailable
- Intended viewports: desktop and mobile down to 320 CSS px
- Source pixel dimensions: 1595 x 638
- Implementation pixel dimensions: unavailable
- CSS viewport and density normalization: unavailable because no browser capture could be produced
- State: Home page product rails with retail badge, persistent action footer, and final View More card

## Full-view comparison evidence

The source screenshot is available, but a browser-rendered implementation capture is not available in the current tool context. A valid side-by-side visual comparison therefore cannot be performed from code inspection alone.

## Focused region comparison evidence

Not performed. The required card image area, badge, and split action footer cannot be visually compared without an implementation screenshot at the same viewport and state.

## Findings

- [Blocked] Browser-rendered evidence is missing.
  - Location: Home product sliders at desktop and 320 px mobile widths.
  - Evidence: source screenshot is available; matching implementation screenshots are unavailable.
  - Impact: card clipping, equal-height rendering, real database image scaling, and footer alignment cannot be certified visually.
  - Fix: render the Home route with seeded products, capture desktop and 320 px states, then compare them against the source in one combined view.

## Required fidelity surfaces

- Fonts and typography: implemented in the existing RTL type system; not visually certified.
- Spacing and layout rhythm: exact slider/card rules are present in CSS; not visually certified.
- Colors and visual tokens: requested gold, charcoal, and beige values are present; not visually certified.
- Image quality and asset fidelity: the card binds only to `Model.Imageurl`; real output cannot be assessed without database-backed rendering.
- Copy and content: Arabic labels are present in Razor; visual wrapping is not certified.

## Comparison history

- Initial pass: blocked before comparison because no implementation screenshot could be captured.
- P0/P1/P2 iterations: none; no valid visual comparison artifact was available.

## Implementation checklist

- [x] Native horizontal swipe rail with hidden scrollbar and mandatory snap.
- [x] Fixed 260 px desktop card width and 180 px mobile width.
- [x] Uniform 1 / 1.1 media wrapper and contained database image.
- [x] Retail-only availability badge.
- [x] Permanently visible 70/30 split action footer.
- [x] Final View More card in each Home product rail.
- [ ] Browser capture and side-by-side visual comparison.

final result: blocked
