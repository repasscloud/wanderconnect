import React from "react";
import {
  AbsoluteFill,
  Img,
  interpolate,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { MapPin, MessageCircle, Search, User, Users } from "lucide-react";

// Shared monochrome building blocks for the feature videos.
export const INK = "#0A0A0A";
export const MUTED = "#6B6B6B";
export const LINE = "#E5E5E5";
export const SOFT = "#F4F4F4";
export const WHITE = "#FFFFFF";
export const VIOLET = "#7228E8";
export const FONT = "'Helvetica Neue', Helvetica, Arial, sans-serif";

// Video background behind the phone. Black by default; the "-Colour" versions provide a theme colour.
export const BgContext = React.createContext(INK);
export const useBg = () => React.useContext(BgContext);

// How far captions sit from the top and the end card's link from the bottom. The -TikTok and -Shorts versions push both
// inward so the apps' tabs and caption overlays don't cover them.
export const SafeArea = React.createContext({ top: 72, bottom: 150 });

// Black text on light backgrounds (e.g. Solar yellow), white otherwise. Accepts #rrggbb or rgb()/rgba().
export const textOn = (bg: string) => {
  const hex = bg.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i);
  const rgb = hex ? hex.slice(1).map((h) => parseInt(h, 16)) : (bg.match(/[\d.]+/g) ?? []).slice(0, 3).map(Number);
  const [r, g, b] = rgb.length === 3 ? rgb : [0, 0, 0];
  return 0.299 * r + 0.587 * g + 0.114 * b > 170 ? INK : WHITE;
};

export const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

export const SCREEN_W = 390;
export const SCREEN_H = 844;
export const PHONE_SCALE = 2.0;
export const PHONE_LEFT = (1080 - SCREEN_W * PHONE_SCALE) / 2 - 12;
export const PHONE_TOP = 190;

// Every profile tile is exactly this size, whether it has a photo or not.
export const TILE_W = 170;
export const TILE_H = 270;
export const TILE_GAP = 10;
export const TILE_PHOTO_H = 170;

// One voiceover line, in frames
export type Cue = { from: number; to: number; text: string };

export type Profile = {
  name: string;
  age: number;
  photo?: string; // omitted = photo-free tile
  quote?: string;
  intentions: string[];
  area: string;
};

export const Badge = ({ src, size = 16 }: { src: string; size?: number }) => (
  <Img src={staticFile(`badges/${src}.svg`)} style={{ width: size, height: size, flexShrink: 0 }} />
);

export const StatusBar = () => (
  <div
    style={{
      height: 50,
      display: "flex",
      justifyContent: "space-between",
      alignItems: "center",
      padding: "0 28px",
      fontWeight: 600,
      fontSize: 15,
      color: INK,
    }}
  >
    <span>9:41</span>
    <span style={{ letterSpacing: 2, fontSize: 12 }}>●●● ▮</span>
  </div>
);

export const Chip = ({ label, on = false, off = false }: { label: string; on?: boolean; off?: boolean }) => (
  <span
    style={{
      fontSize: 11,
      fontWeight: 600,
      padding: "4px 9px",
      borderRadius: 20,
      border: `1px solid ${on ? INK : LINE}`,
      background: on ? INK : WHITE,
      color: on ? WHITE : off ? MUTED : INK,
      textDecoration: off ? "line-through" : undefined,
      whiteSpace: "nowrap",
      display: "inline-flex",
      alignItems: "center",
      lineHeight: 1,
    }}
  >
    {label}
  </span>
);

export const ProfileTile = ({ p }: { p: Profile }) => {
  if (!p.photo) {
    return (
      <div
        style={{
          width: TILE_W,
          height: TILE_H,
          borderRadius: 16,
          overflow: "hidden",
          background: INK,
          color: WHITE,
          padding: 14,
          display: "flex",
          flexDirection: "column",
          boxSizing: "border-box",
        }}
      >
        <div style={{ fontSize: 30, lineHeight: 1, color: "#8A8A8A" }}>“</div>
        <div style={{ fontFamily: "Georgia, 'Times New Roman', serif", fontSize: 19, lineHeight: 1.22, flex: 1 }}>{p.quote}</div>
        <div style={{ fontSize: 15, fontWeight: 700, display: "flex", alignItems: "center", gap: 5 }}>
          {p.name}, {p.age} <Badge src="id-verified" size={14} />
        </div>
        <div style={{ fontSize: 11, color: "#A3A3A3", marginTop: 4 }}>{p.intentions.join(" · ")}</div>
      </div>
    );
  }
  return (
    <div
      style={{
        width: TILE_W,
        height: TILE_H,
        borderRadius: 16,
        overflow: "hidden",
        background: WHITE,
        border: `1px solid ${LINE}`,
        boxSizing: "border-box",
      }}
    >
      <Img
        src={staticFile(p.photo)}
        style={{ width: "100%", height: TILE_PHOTO_H, objectFit: "cover", objectPosition: "top", display: "block" }}
      />
      <div style={{ padding: "10px 12px" }}>
        <div style={{ fontSize: 15, fontWeight: 700, color: INK, display: "flex", alignItems: "center", gap: 4 }}>
          {p.name}, {p.age} <Badge src="photo-verified" size={14} /> <Badge src="id-verified" size={14} />
        </div>
        <div style={{ display: "flex", gap: 4, margin: "8px 0 7px", overflow: "hidden" }}>
          {p.intentions.map((t) => (
            <Chip key={t} label={t} />
          ))}
        </div>
        <div style={{ fontSize: 11, color: MUTED, display: "flex", alignItems: "center", gap: 3 }}>
          <MapPin size={11} /> {p.area}
        </div>
      </div>
    </div>
  );
};

export const tilePos = (index: number, top: number) => ({
  x: 20 + (index % 2) * (TILE_W + TILE_GAP),
  y: top + Math.floor(index / 2) * (TILE_H + TILE_GAP),
});

export const Tap = ({ x, y, at }: { x: number; y: number; at: number }) => {
  const frame = useCurrentFrame();
  const t = frame - (at - 6);
  if (t < 0 || t > 22) return null;
  const size = interpolate(t, [0, 22], [26, 80]);
  const opacity = interpolate(t, [0, 5, 22], [0, 0.5, 0]);
  return (
    <div
      style={{
        position: "absolute",
        left: x - size / 2,
        top: y - size / 2,
        width: size,
        height: size,
        borderRadius: size,
        background: "rgba(128,128,128,0.45)",
        border: "2px solid rgba(255,255,255,0.9)",
        boxShadow: "0 0 0 1px rgba(10,10,10,0.35)",
        opacity,
      }}
    />
  );
};

export const Caption = ({ text, from, to }: { text: string; from: number; to: number }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [from, from + 12, to - 10, to], [0, 1, 1, 0], clamp);
  const y = interpolate(frame, [from, from + 12], [16, 0], clamp);
  return (
    <div
      style={{
        position: "absolute",
        top: React.useContext(SafeArea).top,
        width: "100%",
        textAlign: "center",
        color: textOn(useBg()),
        fontFamily: FONT,
        fontSize: 54,
        fontWeight: 400,
        letterSpacing: -1.5,
        opacity,
        transform: `translateY(${y}px)`,
      }}
    >
      {/* Black pill keeps the line readable if a zoomed phone slides underneath */}
      <span style={{ background: useBg(), padding: "6px 28px", borderRadius: 40 }}>{text}</span>
    </div>
  );
};

// Two-line opening statement on black: a light line, then a bold one
export const Hook = ({ lines, until }: { lines: [string, string]; until: number }) => {
  const frame = useCurrentFrame();
  const out = interpolate(frame, [until - 12, until], [1, 0], clamp);
  return (
    <AbsoluteFill style={{ justifyContent: "center", padding: 90, opacity: out }}>
      {lines.map((l, i) => (
        <div
          key={l}
          style={{
            fontFamily: FONT,
            fontSize: 100,
            lineHeight: 1.08,
            fontWeight: i === 1 ? 700 : 300,
            letterSpacing: -3,
            color: WHITE,
            opacity: interpolate(frame, [i * 14, i * 14 + 12], [0, 1], clamp),
            transform: `translateY(${interpolate(frame, [i * 14, i * 14 + 12], [24, 0], clamp)}px)`,
          }}
        >
          {l}
        </div>
      ))}
    </AbsoluteFill>
  );
};

export const EndCard = ({ at, tagline, credit }: { at: number; tagline: string; credit?: string }) => {
  const frame = useCurrentFrame();
  if (frame < at) return null;
  const fade = (start: number) => ({
    opacity: interpolate(frame, [start, start + 15], [0, 1], clamp),
    transform: `translateY(${interpolate(frame, [start, start + 15], [20, 0], clamp)}px)`,
  });
  return (
    <AbsoluteFill
      style={{
        background: useBg(),
        opacity: interpolate(frame, [at, at + 12], [0, 1], clamp),
        justifyContent: "center",
        alignItems: "center",
        fontFamily: FONT,
        color: WHITE,
      }}
    >
      <Img src={staticFile("logo.svg")} style={{ width: 520, ...fade(at + 8) }} />
      <div style={{ fontSize: 72, fontWeight: 300, letterSpacing: -2, marginTop: 90, ...fade(at + 22) }}>{tagline}</div>
      <div style={{ position: "absolute", bottom: React.useContext(SafeArea).bottom, textAlign: "center", ...fade(at + 36) }}>
        <div style={{ fontSize: 46, fontWeight: 700 }}>swoovr.com</div>
        <div style={{ fontSize: 26, letterSpacing: 3, color: "rgba(255,255,255,0.7)", marginTop: 14 }}>IN DEVELOPMENT · ADULTS 18+</div>
      </div>
      {credit && (
        <div style={{ position: "absolute", bottom: 60, width: "100%", textAlign: "center", fontSize: 20, color: "rgba(255,255,255,0.55)", ...fade(at + 36) }}>
          {credit}
        </div>
      )}
    </AbsoluteFill>
  );
};

// Phone frame that springs up at `inAt`. Children are laid out in 390×844 points.
// `concept` is the label in the left margin that marks the screen as a planned design, not a real member
export const Phone = ({
  inAt,
  zoom = 1,
  origin = "50% 50%",
  concept = "Design concept · illustrative profiles",
  children,
}: {
  inAt: number;
  zoom?: number;
  origin?: string;
  concept?: string | false;
  children: React.ReactNode;
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const rise = spring({ frame: frame - inAt, fps, config: { damping: 18 } });
  const bg = useBg();
  return (
    <>
      <div
        style={{
          position: "absolute",
          left: PHONE_LEFT,
          top: PHONE_TOP + (1 - rise) * 2000,
          width: SCREEN_W * PHONE_SCALE + 24,
          height: SCREEN_H * PHONE_SCALE + 24,
          background: "#1C1C1C",
          border: "2px solid #333",
          borderRadius: 118,
          padding: 10,
          transform: `scale(${zoom})`,
          transformOrigin: origin,
        }}
      >
        <div style={{ width: SCREEN_W * PHONE_SCALE, height: SCREEN_H * PHONE_SCALE, borderRadius: 106, overflow: "hidden" }}>
          <div
            style={{
              width: SCREEN_W,
              height: SCREEN_H,
              transform: `scale(${PHONE_SCALE})`,
              transformOrigin: "top left",
              position: "relative",
              overflow: "hidden",
              background: WHITE,
              fontFamily: FONT,
            }}
          >
            {children}
            <div style={{ position: "absolute", top: 11, left: SCREEN_W / 2 - 60, width: 120, height: 34, borderRadius: 20, background: INK }} />
          </div>
        </div>
      </div>
      {concept && (
        <div
          style={{
            position: "absolute",
            left: PHONE_LEFT / 2,
            top: 1180,
            transform: "translate(-50%, -50%) rotate(-90deg)",
            whiteSpace: "nowrap",
            fontFamily: FONT,
            fontSize: 24,
            fontWeight: 500,
            letterSpacing: 3,
            textTransform: "uppercase",
            color: textOn(bg),
            background: bg,
            padding: "6px 14px",
            borderRadius: 20,
            opacity: 0.75 * rise,
          }}
        >
          {concept}
        </div>
      )}
    </>
  );
};

export const TabBar = ({ active = "Explore" }: { active?: string }) => (
  <div
    style={{
      position: "absolute",
      bottom: 0,
      left: 0,
      right: 0,
      height: 80,
      background: WHITE,
      borderTop: `1px solid ${LINE}`,
      display: "flex",
      justifyContent: "space-around",
      paddingTop: 10,
      fontSize: 11,
      color: MUTED,
    }}
  >
    {[
      { label: "Explore", Icon: Search },
      { label: "Connections", Icon: Users },
      { label: "Chats", Icon: MessageCircle },
      { label: "Me", Icon: User },
    ].map(({ label, Icon }) => {
      const on = label === active;
      return (
        <div
          key={label}
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: 4,
            width: 80,
            color: on ? INK : MUTED,
            fontWeight: on ? 700 : 400,
          }}
        >
          <Icon size={22} strokeWidth={on ? 2.25 : 1.75} />
          {label}
        </div>
      );
    })}
  </div>
);
