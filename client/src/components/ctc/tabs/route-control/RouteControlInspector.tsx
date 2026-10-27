import { InspectorPanel } from "@/components/ctc/inspector/InspectorPanel";
import type { RouteDefinition } from "@/components/ctc/diagram/TrackDiagram";
import { RouteStatusIndicator } from "@/components/ctc/tabs/route-control/RouteStatusIndicator";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import type { CtcSnapshot, InterlockingStationStatus } from "@/gen/ctc_pb";
import { getRouteCategory, ROUTE_CATEGORIES } from "@/lib/routeCategory";

type Props = {
  active: boolean;
  routes: readonly RouteDefinition[];
  circuitsById: CtcSnapshot["circuitsById"] | undefined;
  station: InterlockingStationStatus | undefined;
  onCancelRoute: (routeId: string) => void;
  isSendingRoute: boolean;
};

export function RouteControlInspector({ routes, circuitsById, station, onCancelRoute, isSendingRoute, ...panelProps }: Props) {
  const routesById = station?.isInitialized && station.hasOutput ? station.routesById : undefined;
  const routeGroups = ROUTE_CATEGORIES.map((category) => ({
    ...category,
    routes: routes.filter((route) => getRouteCategory(route.category) === category.value),
  })).filter((group) => group.routes.length > 0);

  return (
    <InspectorPanel {...panelProps} id="route-control-inspector" title="進路制御のインスペクタ">
      {routes.length > 0 ? (
        <Accordion
          multiple
          defaultValue={ROUTE_CATEGORIES.map((category) => category.value)}
          aria-label="進路カテゴリ"
          className="px-2"
        >
          {routeGroups.map((group) => (
            <AccordionItem key={group.value} value={group.value} data-route-category={group.value} className="border-zinc-700">
              <AccordionTrigger className="items-center rounded-none py-2 text-zinc-300 hover:no-underline">
                <span>
                  {group.label}
                  <span className="ml-2 text-xs font-normal text-zinc-500">{group.routes.length}</span>
                </span>
              </AccordionTrigger>
              <AccordionContent className="pb-2">
                <ul aria-label={`${group.label}の進路状況`} className="ml-auto w-60 max-w-full space-y-1">
                  {group.routes.map((route) => (
                    <li key={route.routeId} data-route-id={route.routeId} title={route.routeId}>
                      <RouteStatusIndicator
                        originName={circuitsById?.[route.startTrackCircuitId]?.displayName || route.startTrackCircuitId}
                        destinationName={circuitsById?.[route.destinationTrackCircuitId]?.displayName || route.destinationTrackCircuitId}
                        route={routesById?.[route.routeId]}
                        onCancel={() => onCancelRoute(route.routeId)}
                        disabled={isSendingRoute}
                        className="text-sm"
                      />
                    </li>
                  ))}
                </ul>
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      ) : (
        <p className="p-4 text-sm text-zinc-400">この駅には進路定義がありません。</p>
      )}
    </InspectorPanel>
  );
}
