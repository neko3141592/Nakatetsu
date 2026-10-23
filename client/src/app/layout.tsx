import type { Metadata } from "next";
import { BIZ_UDGothic } from "next/font/google";
import "./globals.css";

const bizUDGothic = BIZ_UDGothic({
  variable: "--font-biz-ud-gothic",
  weight: ["400", "700"],
  display: "swap",
  preload: false,
});

export const metadata: Metadata = {
  title: "Nakatetsu CTC",
  description: "中鉄の列車在線情報・ダイヤ管理",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="ja"
      className={`${bizUDGothic.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
