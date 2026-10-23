export enum RouteCategory {
  DownArrival = "DownArrival",
  UpArrival = "UpArrival",
  DownDeparture = "DownDeparture",
  UpDeparture = "UpDeparture",
  Shunting = "Shunting",
  Other = "Other",
}

// 進路インスペクタの表示名と並び順。
export const ROUTE_CATEGORIES = [
  { value: RouteCategory.DownArrival, label: "下り場内" },
  { value: RouteCategory.UpArrival, label: "上り場内" },
  { value: RouteCategory.DownDeparture, label: "下り出発" },
  { value: RouteCategory.UpDeparture, label: "上り出発" },
  { value: RouteCategory.Shunting, label: "入換" },
  { value: RouteCategory.Other, label: "その他" },
] as const;

export function getRouteCategory(value: unknown): RouteCategory {
  return ROUTE_CATEGORIES.find((category) => category.value === value)?.value ?? RouteCategory.Other;
}
