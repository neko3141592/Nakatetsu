export enum RouteDirection {
  Up = "Up",
  Down = "Down",
}

export enum RouteKind {
  Departure = "Departure",
  Home = "Home",
  Shunting = "Shunting",
}

// 進路インスペクタの表示名と並び順。
export const ROUTE_CATEGORIES = [
  { direction: RouteDirection.Down, kind: RouteKind.Home, label: "下り場内" },
  { direction: RouteDirection.Up, kind: RouteKind.Home, label: "上り場内" },
  { direction: RouteDirection.Down, kind: RouteKind.Departure, label: "下り出発" },
  { direction: RouteDirection.Up, kind: RouteKind.Departure, label: "上り出発" },
  { direction: RouteDirection.Down, kind: RouteKind.Shunting, label: "下り入換" },
  { direction: RouteDirection.Up, kind: RouteKind.Shunting, label: "上り入換" },
].map((category) => ({ ...category, value: `${category.direction}${category.kind}` }));
