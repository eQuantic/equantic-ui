# Tasks

## 1. A decimal keeps its digits on the way to an integer

- [x] 1.1 A cast to every integer type and a char, and decimal's To<integer>, cut toward zero from the decimal's digits, then the range in .NET's words (#1766)
- [x] 1.2 Prove it on both sides at the edges of every type: `DecimalMathConformanceTests`, failing against main

## 2. Math over a decimal answers as .NET's decimal does

- [x] 2.1 The decimal row answers Abs, Sign, Max, Min and Clamp, with the scale .NET keeps; decimal's statics reach it (#1767)
- [x] 2.2 A Math member over a decimal with no row is refused by the build
- [x] 2.3 Prove it on both sides, the ties of Max and Min and Clamp's refusal included
