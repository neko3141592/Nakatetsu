"use client";

import type { CSSProperties, ReactNode } from "react";

import { ScrollArea } from "@/components/ui/scroll-area";
import { Sidebar, SidebarProvider, SidebarTrigger, useSidebar } from "@/components/ui/sidebar";

const triggerClassName =
  "size-8 rounded-none bg-transparent text-inherit shadow-none transition-none hover:bg-transparent! hover:text-inherit! aria-expanded:bg-transparent! aria-expanded:text-inherit! [&_svg]:rotate-180";

export function InspectorWorkspace({ active, children }: { active: boolean; children: ReactNode }) {
  return (
    <SidebarProvider
      keyboardShortcutEnabled={active}
      className="absolute inset-0 min-h-0! overflow-hidden"
      style={{ "--sidebar-width": "min(19rem, 40vw)" } as CSSProperties}
    >
      {children}
    </SidebarProvider>
  );
}

type Props = {
  id: string;
  title: string;
  active: boolean;
  children: ReactNode;
};

export function InspectorToggle({ id, title, className = "" }: { id: string; title: string; className?: string }) {
  const { open, openMobile, isMobile } = useSidebar();
  const panelOpen = isMobile ? openMobile : open;
  const toggleLabel = `${title}を${panelOpen ? "閉じる" : "開く"}`;

  return (
    <SidebarTrigger
      aria-label={toggleLabel}
      title={toggleLabel}
      aria-controls={id}
      aria-expanded={panelOpen}
      className={`${triggerClassName} text-zinc-200 ${className}`}
    />
  );
}

export function InspectorPanel({ id, title, active, children }: Props) {
  const { open, openMobile, isMobile } = useSidebar();
  const panelOpen = isMobile ? openMobile : open;

  if (!active) return null;

  return (
    <Sidebar
      side="right"
      collapsible="offcanvas"
      className="absolute! inset-y-0! h-full! border-0!"
    >
      <aside
        id={id}
        aria-labelledby={`${id}-title`}
        aria-hidden={!panelOpen}
        inert={!panelOpen}
        className="flex h-full min-h-0 w-full flex-col overflow-hidden bg-zinc-900 text-zinc-200"
      >
        <h2 id={`${id}-title`} className="sr-only">{title}</h2>
        <InspectorToggle id={id} title={title} className="ml-auto md:hidden" />
        <ScrollArea className="min-h-0 w-full flex-1">{children}</ScrollArea>
      </aside>
    </Sidebar>
  );
}
