This is a [Next.js](https://nextjs.org) project bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app).

## Getting Started

First, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

You can start editing the page by modifying `app/page.tsx`. The page auto-updates as you edit the file.

This project uses [`next/font`](https://nextjs.org/docs/app/building-your-application/optimizing/fonts) to automatically optimize and load [Geist](https://vercel.com/font), a new font family for Vercel.

## CTC配線図の在線表示

軌道回路の在線が判明している場合、転轍機の位置情報によって在線表示を消さない。

| 転轍機の状態 | 在線時の表示 |
| --- | --- |
| 定位確定・非転換中 | 定位側が赤 |
| 反位確定・非転換中 | 反位側が赤 |
| 転換中（保持している実位置にかかわらず） | 両側が赤 |
| 位置不明・未対応の位置値 | 両側が赤 |
| 転轍機ID・マップ・位置情報の欠落 | 両側が赤 |

両側の赤は「在線する分岐を特定できない」表示であり、両側の開通を意味しない。
方向表示灯は引き続き転換中と位置不明を区別し、どちらもN/R灯を消灯する。
各線の色は **在線（赤） > 進路予約（黄） > 通常（白）** の順に優先する。
非在線時は転轍機状態にかかわらず予約した側だけ黄色になる。
この規則は取得済みの在線情報に対する表示処理であり、通信仕様・連動判断は変更しない。

`npm test` で実際のSVGのstrokeと状態更新を検証する回帰テストを実行できる。

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.
