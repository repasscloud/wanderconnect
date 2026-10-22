import React from "react";
import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { Check, ChevronLeft, KeyRound, Mail, ScanFace } from "lucide-react";
import { Caption, EndCard, Hook, INK, LINE, MUTED, Phone, SCREEN_W, SOFT, StatusBar, Tap, WHITE, clamp, useBg } from "./ui";
import type { Cue } from "./ui";

// Timeline (30fps)
const PHONE_IN = 50;
const SPLASH = PHONE_IN + 15; // bird starts drawing
const WIPE = SPLASH + 82; // splash wipes up off the sign-in screen
const SIGNIN_IN = SPLASH + 94;
const SIGNUP_TAP = 235;
const SIGNUP_BACK = 345;
const FORGOT_TAP = 370;
const SEND_TAP = 430;
const SENT = 455;
const FORGOT_BACK = 515;
const PASSKEY_TAP = 540;
const CONTINUE_TAP = 575;
const FACE_DONE = 612;
const WELCOME = 632;
const OUTRO = 715;

// The bird from logo.svg, in its own 420×356 box
const BIRD =
  "M139 0C153.755264 7.892353 168.569412 15.313709 182.947464 22.516602C252.37085 57.295238 311.627411 86.980667 305 140C304.202057 150.107193 301.06955 159.719162 296.279846 168.835938C297.106506 168.971268 298.010498 169.491089 298.900879 170.167953C327.374664 144.083389 345.228699 148.062378 367 154C373 156 378 156 384 156C355 164 350 185 339 207C310 270 226 282 97 324C153.7 289.85 197.8 257.1 233.4 227.7C236.604 225.054 238.45 222.35 239.85 218.65C260.85 163.15 218.0 80.86 139 0ZM77 137C120 159 205 163 226 192C259.219879 239.585236 132.02446 263.793365 32.588951 282.71817C21.274101 284.871643 10.3187 286.956696 0 289C79 260 166 226 180 207C188.996353 193.205597 164.180161 181.929153 134.46019 168.424454C114.626785 159.41217 92.609497 149.407547 77 137Z";

const Bird = ({ width, color = INK, draw = 1, fill = 1 }: { width: number; color?: string; draw?: number; fill?: number }) => (
  <svg viewBox="140 30 420 356" style={{ width, height: (width * 356) / 420, display: "block", overflow: "visible" }}>
    <path
      d={BIRD}
      transform="translate(145 35.03125) scale(1.067708)"
      fillRule="evenodd"
      pathLength={1}
      fill={color}
      fillOpacity={fill}
      stroke={color}
      strokeWidth={3 * (1 - fill)}
      strokeDasharray={1}
      strokeDashoffset={1 - draw}
    />
  </svg>
);

// Same easing as the HTML prototype: quick start, soft landing
const out = Easing.bezier(0.2, 0.8, 0.2, 1);
const inOut = Easing.bezier(0.76, 0, 0.24, 1);
const ease = (frame: number, at: number, dur = 18, easing = out) => interpolate(frame, [at, at + dur], [0, 1], { ...clamp, easing });

// Elements arrive one after another: fade in while rising 18pt
const rise = (frame: number, start: number, d: number) => {
  const p = ease(frame, start + d * 2, 20);
  return { opacity: p, transform: `translateY(${(1 - p) * 18}px)` };
};

const typed = (frame: number, text: string, from: number, to: number) =>
  text.slice(0, Math.round(interpolate(frame, [from, to], [0, text.length], clamp)));

// Absolutely placed row (screen points), staggered in from `start`
const Row = ({ top, d, start, children, style }: { top: number; d: number; start: number; children: React.ReactNode; style?: React.CSSProperties }) => {
  const frame = useCurrentFrame();
  return <div style={{ position: "absolute", top, left: 28, right: 28, ...rise(frame, start, d), ...style }}>{children}</div>;
};

const Field = ({ label, value, placeholder, focus, secret, trailing }: { label: string; value: string; placeholder: string; focus?: boolean; secret?: boolean; trailing?: string }) => {
  const frame = useCurrentFrame();
  const caret = focus && Math.floor(frame / 15) % 2 === 0;
  return (
    <div
      style={{
        position: "relative",
        height: 56,
        border: `1px solid ${focus ? INK : LINE}`,
        boxShadow: focus ? "0 0 0 3px rgba(10,10,10,0.06)" : undefined,
        borderRadius: 14,
        padding: "9px 16px 0",
        boxSizing: "border-box",
        background: WHITE,
      }}
    >
      <div style={{ fontSize: 11, color: MUTED, letterSpacing: 0.3, textTransform: "uppercase" }}>{label}</div>
      <div style={{ fontSize: 16, marginTop: 3, color: value ? INK : "#B5B5B5", height: 20, display: "flex", alignItems: "center" }}>
        {value ? (secret ? "•".repeat(value.length) : value) : placeholder}
        {caret && <span style={{ width: 1.5, height: 19, background: INK, marginLeft: 1 }} />}
      </div>
      {trailing && <div style={{ position: "absolute", right: 16, top: 19, fontSize: 13, fontWeight: 500, color: INK }}>{trailing}</div>}
    </div>
  );
};

const Button = ({ children, kind = "primary", loading = false, faded = false }: { children: React.ReactNode; kind?: "primary" | "outline"; loading?: boolean; faded?: boolean }) => {
  const frame = useCurrentFrame();
  const dark = kind === "primary";
  return (
    <div
      style={{
        height: 54,
        borderRadius: 14,
        border: `1px solid ${dark ? INK : LINE}`,
        background: dark ? INK : WHITE,
        color: dark ? WHITE : INK,
        fontSize: 16,
        fontWeight: 600,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: 10,
        opacity: faded ? 0.25 : 1,
      }}
    >
      {loading ? (
        <div
          style={{
            width: 18,
            height: 18,
            borderRadius: 18,
            border: "2px solid rgba(255,255,255,0.3)",
            borderTopColor: WHITE,
            boxSizing: "border-box",
            transform: `rotate(${frame * 17}deg)`,
          }}
        />
      ) : (
        children
      )}
    </div>
  );
};

const AppleLogo = () => (
  <svg viewBox="0 0 24 24" width={19} height={19} fill="currentColor">
    <path d="M16.37 1.43c0 1.14-.5 2.27-1.18 3.08-.74.9-1.99 1.57-2.99 1.57-.12 0-.23-.02-.3-.03-.01-.06-.04-.22-.04-.39 0-1.15.57-2.27 1.2-2.98.81-.94 2.15-1.64 3.25-1.68.03.13.06.28.06.43Zm4.56 15.71c-.03.07-.46 1.58-1.52 3.12-.94 1.34-1.94 2.71-3.43 2.71-1.52 0-1.9-.88-3.63-.88-1.7 0-2.3.91-3.67.91-1.38 0-2.33-1.26-3.43-2.8C4 18.38 2.96 15.57 2.96 12.92c0-4.28 2.8-6.55 5.55-6.55 1.45 0 2.68.95 3.6.95.87 0 2.22-1.01 3.9-1.01.62 0 2.89.06 4.38 2.19-.13.09-2.39 1.37-2.39 4.19 0 3.26 2.86 4.42 2.96 4.45Z" />
  </svg>
);

const GoogleLogo = () => (
  <svg viewBox="0 0 48 48" width={19} height={19}>
    <path fill="#FFC107" d="M43.6 20.1H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 8 3l5.6-5.6C34 6.1 29.3 4 24 4 13 4 4 13 4 24s9 20 20 20 20-9 20-20c0-1.3-.1-2.6-.4-3.9Z" />
    <path fill="#FF3D00" d="m6.3 14.7 6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 8 3l5.6-5.6C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7Z" />
    <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2A11.9 11.9 0 0 1 24 36c-5.2 0-9.6-3.3-11.3-7.9l-6.5 5C9.5 39.6 16.2 44 24 44Z" />
    <path fill="#1976D2" d="M43.6 20.1H42V20H24v8h11.3a12 12 0 0 1-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.6-.4-3.9Z" />
  </svg>
);

const Title = ({ children }: { children: React.ReactNode }) => (
  <div style={{ fontSize: 32, lineHeight: 1.05, letterSpacing: -1, fontWeight: 700, color: INK }}>{children}</div>
);
const Sub = ({ children }: { children: React.ReactNode }) => <div style={{ fontSize: 15, lineHeight: 1.4, color: MUTED }}>{children}</div>;

const Back = ({ start }: { start: number }) => (
  <Row top={58} d={0} start={start} style={{ left: 14 }}>
    <ChevronLeft size={28} color={INK} strokeWidth={1.75} />
  </Row>
);

const Footer = ({ start }: { start: number }) => (
  <Row top={772} d={12} start={start} style={{ textAlign: "center", fontSize: 11, lineHeight: 1.6, color: MUTED }}>
    In development · Starting in Adelaide · Adults 18+
    <div style={{ fontFamily: "ui-monospace, 'SF Mono', Menlo, monospace", fontSize: 10.5, color: "#9A9A9A" }}>v0.1.0-dev · design concept</div>
  </Row>
);

// Layout of the sign-in screen, so the taps land on the right rows
const SI = { forgot: 342, signIn: 372, passkey: 474, create: 676 };

const SignIn = () => {
  const frame = useCurrentFrame();
  const s = SIGNIN_IN;
  return (
    <AbsoluteFill style={{ background: WHITE }}>
      <StatusBar />
      <Row top={70} d={0} start={s} style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <Bird width={34} />
        <span style={{ fontSize: 19, letterSpacing: 3, fontWeight: 700, color: INK }}>swoovr</span>
      </Row>
      <Row top={128} d={1} start={s}>
        <Title>Welcome back.</Title>
      </Row>
      <Row top={172} d={2} start={s}>
        <Sub>Sign in to dating, friendship and things to do.</Sub>
      </Row>
      <Row top={212} d={3} start={s}>
        <Field label="Username or email" value="" placeholder="you@example.com" />
      </Row>
      <Row top={276} d={4} start={s}>
        <Field label="Password" value="" placeholder="••••••••" trailing="Show" />
      </Row>
      <Row top={SI.forgot} d={5} start={s} style={{ textAlign: "right", fontSize: 13, color: INK }}>
        <span style={{ borderBottom: `1px solid ${LINE}` }}>Forgot password?</span>
      </Row>
      <Row top={SI.signIn} d={6} start={s}>
        <Button>Sign in</Button>
      </Row>
      <Row top={444} d={7} start={s} style={{ display: "flex", alignItems: "center", gap: 12, fontSize: 12, letterSpacing: 0.4, color: MUTED }}>
        <div style={{ flex: 1, height: 1, background: LINE }} />
        OR
        <div style={{ flex: 1, height: 1, background: LINE }} />
      </Row>
      <Row top={SI.passkey} d={8} start={s}>
        <Button kind="outline">
          <KeyRound size={19} strokeWidth={1.8} /> Sign in with a passkey
        </Button>
      </Row>
      <Row top={538} d={9} start={s}>
        <Button>
          <AppleLogo /> Sign in with Apple
        </Button>
      </Row>
      <Row top={602} d={10} start={s}>
        <Button kind="outline">
          <GoogleLogo /> Sign in with Google
        </Button>
      </Row>
      <Row top={SI.create} d={11} start={s} style={{ textAlign: "center", fontSize: 14, color: MUTED }}>
        New to Swoovr? <span style={{ color: INK, fontWeight: 600 }}>Create an account</span>
      </Row>
      <Footer start={s} />
      {/* Splash on top until it wipes away */}
      {frame < WIPE + 30 && <Splash />}
    </AbsoluteFill>
  );
};

const Splash = () => {
  const frame = useCurrentFrame();
  const s = SPLASH;
  const wipe = ease(frame, WIPE, 27, inOut);
  return (
    <AbsoluteFill
      style={{
        background: INK,
        alignItems: "center",
        justifyContent: "center",
        clipPath: `inset(0 0 ${wipe * 100}% 0 round 0 0 48px 48px)`,
      }}
    >
      <div style={{ position: "absolute", top: 0, left: 0, right: 0 }}>
        <StatusBarWhite />
      </div>
      <Bird width={132} color={WHITE} draw={ease(frame, s, 39, inOut)} fill={ease(frame, s + 37, 15)} />
      <div style={{ display: "flex", overflow: "hidden", marginTop: 22, paddingLeft: 9 }}>
        {"swoovr".split("").map((c, i) => (
          <span
            key={i}
            style={{
              display: "inline-block",
              fontSize: 30,
              fontWeight: 700,
              letterSpacing: 9,
              color: WHITE,
              transform: `translateY(${(1 - ease(frame, s + 36 + i * 1.6, 21)) * 110}%)`,
            }}
          >
            {c}
          </span>
        ))}
      </div>
      <div style={{ marginTop: 34, width: 120, height: 1, background: "rgba(255,255,255,0.18)" }}>
        <div style={{ height: 1, background: WHITE, transform: `scaleX(${ease(frame, s + 27, 45, inOut)})`, transformOrigin: "left" }} />
      </div>
      <div style={{ position: "absolute", bottom: 52, fontSize: 12, letterSpacing: 0.4, color: "rgba(255,255,255,0.55)", opacity: ease(frame, s + 45, 18) }}>
        Dating · Friendship · Activities
      </div>
    </AbsoluteFill>
  );
};

const StatusBarWhite = () => (
  <div style={{ color: WHITE }}>
    <div style={{ height: 50, display: "flex", justifyContent: "space-between", alignItems: "center", padding: "0 28px", fontWeight: 600, fontSize: 15 }}>
      <span>9:41</span>
      <span style={{ letterSpacing: 2, fontSize: 12 }}>●●● ▮</span>
    </div>
  </div>
);

// Sign-up: fill in, confirm 18+, then back to sign in
const SU = { name: 196, email: 260, password: 324, adult: 398, create: 478, signIn: 560 };
const NAME_TYPE = SIGNUP_TAP + 26;
const EMAIL_TYPE = NAME_TYPE + 20;
const PASS_TYPE = EMAIL_TYPE + 30;
const ADULT_TAP = PASS_TYPE + 22;

const SignUp = () => {
  const frame = useCurrentFrame();
  const s = SIGNUP_TAP + 6;
  const ticked = frame >= ADULT_TAP;
  const tick = ease(frame, ADULT_TAP, 10);
  return (
    <AbsoluteFill style={{ background: WHITE }}>
      <StatusBar />
      <Back start={s} />
      <Row top={100} d={1} start={s}>
        <Title>Create your account.</Title>
      </Row>
      <Row top={144} d={2} start={s}>
        <Sub>Swoovr is in development, starting in Adelaide.</Sub>
      </Row>
      <Row top={SU.name} d={3} start={s}>
        <Field label="First name" value={typed(frame, "Ana", NAME_TYPE, NAME_TYPE + 8)} placeholder="Your first name" focus={frame >= NAME_TYPE - 6 && frame < EMAIL_TYPE} />
      </Row>
      <Row top={SU.email} d={4} start={s}>
        <Field label="Email" value={typed(frame, "ana@example.com", EMAIL_TYPE, EMAIL_TYPE + 22)} placeholder="you@example.com" focus={frame >= EMAIL_TYPE && frame < PASS_TYPE} />
      </Row>
      <Row top={SU.password} d={5} start={s}>
        <Field label="Password" value={typed(frame, "abcdefghij", PASS_TYPE, PASS_TYPE + 14)} placeholder="At least 8 characters" secret focus={frame >= PASS_TYPE && frame < ADULT_TAP} trailing="Show" />
      </Row>
      <Row top={SU.adult} d={6} start={s} style={{ display: "flex", alignItems: "center", gap: 12, fontSize: 15, color: INK }}>
        <div
          style={{
            width: 24,
            height: 24,
            borderRadius: 7,
            border: `1.5px solid ${ticked ? INK : "#BDBDBD"}`,
            background: ticked ? INK : WHITE,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            transform: `scale(${1 + Math.sin(tick * Math.PI) * 0.15})`,
          }}
        >
          {ticked && <Check size={16} color={WHITE} strokeWidth={3} style={{ opacity: tick }} />}
        </div>
        I'm 18 or over
      </Row>
      <Row top={432} d={7} start={s} style={{ fontSize: 12, lineHeight: 1.5, color: MUTED }}>
        Swoovr is for adults 18+. By continuing you agree to the Terms and Privacy Policy.
      </Row>
      <Row top={SU.create} d={8} start={s}>
        <Button faded={!ticked}>Create account</Button>
      </Row>
      <Row top={SU.signIn} d={9} start={s} style={{ textAlign: "center", fontSize: 14, color: MUTED }}>
        Already have an account? <span style={{ color: INK, fontWeight: 600 }}>Sign in</span>
      </Row>
      <Footer start={s} />
    </AbsoluteFill>
  );
};

// Forgot password: type the email, send, then the "check your email" state
const FP = { send: 350 };
const FP_TYPE = FORGOT_TAP + 22;

const Forgot = () => {
  const frame = useCurrentFrame();
  const sent = frame >= SENT;
  const s = sent ? SENT : FORGOT_TAP + 6;
  const Icon = sent ? Mail : KeyRound;
  return (
    <AbsoluteFill style={{ background: WHITE }}>
      <StatusBar />
      <Back start={FORGOT_TAP + 6} />
      <Row top={104} d={1} start={s}>
        <div style={{ width: 56, height: 56, borderRadius: 56, background: sent ? INK : SOFT, display: "flex", alignItems: "center", justifyContent: "center" }}>
          <Icon size={24} color={sent ? WHITE : INK} strokeWidth={1.8} />
        </div>
      </Row>
      <Row top={178} d={2} start={s}>
        <Title>{sent ? "Check your email." : "Forgot password?"}</Title>
      </Row>
      <Row top={222} d={3} start={s}>
        <Sub>
          {sent ? (
            <>
              If an account matches <span style={{ color: INK }}>ana@example.com</span>, a reset link is on its way.
            </>
          ) : (
            "Enter your email and we'll send you a link to reset it."
          )}
        </Sub>
      </Row>
      {sent ? (
        <Row top={282} d={4} start={s}>
          <Button kind="outline">
            <Mail size={19} strokeWidth={1.8} /> Open email app
          </Button>
        </Row>
      ) : (
        <Row top={282} d={4} start={s}>
          <Field label="Email" value={typed(frame, "ana@example.com", FP_TYPE, FP_TYPE + 22)} placeholder="you@example.com" focus={frame >= FP_TYPE - 6 && frame < SEND_TAP} />
        </Row>
      )}
      <Row top={FP.send} d={5} start={s}>
        <Button loading={frame >= SEND_TAP && !sent}>{sent ? "Back to sign in" : "Send reset link"}</Button>
      </Row>
      <Footer start={FORGOT_TAP + 6} />
    </AbsoluteFill>
  );
};

// System passkey sheet, then Face ID
const SHEET_H = 330;
const SHEET_CONTINUE = 844 - SHEET_H + 238;

const PasskeySheet = () => {
  const frame = useCurrentFrame();
  const up = ease(frame, PASSKEY_TAP + 4, 18) - ease(frame, FACE_DONE + 10, 14, inOut);
  if (frame < PASSKEY_TAP) return null;
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ background: `rgba(10,10,10,${0.35 * up})` }} />
      <div
        style={{
          position: "absolute",
          left: 8,
          right: 8,
          bottom: 8,
          height: SHEET_H,
          borderRadius: 38,
          background: WHITE,
          transform: `translateY(${(1 - up) * (SHEET_H + 20)}px)`,
          boxShadow: "0 -10px 40px rgba(0,0,0,0.18)",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          padding: "22px 24px 0",
          boxSizing: "border-box",
        }}
      >
        <div style={{ fontSize: 13, fontWeight: 600, color: MUTED }}>Sign In</div>
        <div style={{ width: 64, height: 64, borderRadius: 16, background: INK, marginTop: 18, display: "flex", alignItems: "center", justifyContent: "center" }}>
          <Bird width={40} color={WHITE} />
        </div>
        <div style={{ fontSize: 19, fontWeight: 700, color: INK, marginTop: 14, textAlign: "center" }}>Use your passkey for swoovr.com?</div>
        <div style={{ fontSize: 14, color: MUTED, marginTop: 6 }}>ana@example.com</div>
        <div style={{ position: "absolute", top: 238 - 27, left: 24, right: 24 }}>
          <Button>Continue</Button>
        </div>
      </div>
      <FaceId />
    </AbsoluteFill>
  );
};

const FaceId = () => {
  const frame = useCurrentFrame();
  const t = CONTINUE_TAP + 6;
  const show = ease(frame, t, 10) - ease(frame, FACE_DONE + 12, 8);
  if (show <= 0) return null;
  const done = frame >= FACE_DONE - 10;
  const pulse = 1 + Math.sin((frame - t) / 3) * 0.04;
  return (
    <div
      style={{
        position: "absolute",
        left: SCREEN_W / 2 - 75,
        top: 300,
        width: 150,
        height: 150,
        borderRadius: 34,
        background: "rgba(30,30,30,0.86)",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 12,
        opacity: show,
        transform: `scale(${0.9 + 0.1 * show})`,
        color: WHITE,
      }}
    >
      {done ? (
        <Check size={58} strokeWidth={2.2} style={{ transform: `scale(${0.6 + 0.4 * ease(frame, FACE_DONE - 10, 10)})` }} />
      ) : (
        <ScanFace size={58} strokeWidth={1.4} style={{ transform: `scale(${pulse})` }} />
      )}
      <div style={{ fontSize: 14, fontWeight: 500 }}>Face ID</div>
    </div>
  );
};

// After a passkey sign-in: a quick "you're in", then on into the app
const Welcome = () => {
  const frame = useCurrentFrame();
  const s = WELCOME;
  const ring = ease(frame, s + 2, 24, inOut);
  return (
    <AbsoluteFill style={{ background: WHITE, alignItems: "center" }}>
      <div style={{ alignSelf: "stretch" }}>
        <StatusBar />
      </div>
      <div style={{ marginTop: 200, position: "relative", width: 96, height: 96 }}>
        <svg width={96} height={96} viewBox="0 0 96 96" style={{ position: "absolute" }}>
          <circle cx={48} cy={48} r={46} fill="none" stroke={LINE} strokeWidth={1.5} />
          <circle
            cx={48}
            cy={48}
            r={46}
            fill="none"
            stroke={INK}
            strokeWidth={2}
            pathLength={1}
            strokeDasharray={1}
            strokeDashoffset={1 - ring}
            transform="rotate(-90 48 48)"
          />
          <path
            d="M30 49 L43 62 L67 36"
            fill="none"
            stroke={INK}
            strokeWidth={3}
            strokeLinecap="round"
            strokeLinejoin="round"
            pathLength={1}
            strokeDasharray={1}
            strokeDashoffset={1 - ease(frame, s + 18, 14)}
          />
        </svg>
      </div>
      <div style={{ ...rise(frame, s + 14, 0), marginTop: 34 }}>
        <Title>You're in, Ana.</Title>
      </div>
      <div style={{ ...rise(frame, s + 14, 1), marginTop: 10, textAlign: "center", padding: "0 28px" }}>
        <Sub>Signed in with your passkey. No password needed.</Sub>
      </div>
      <div style={{ ...rise(frame, s + 14, 3), marginTop: 44, display: "flex", flexDirection: "column", alignItems: "center", gap: 14 }}>
        <div style={{ width: 120, height: 1, background: LINE }}>
          <div style={{ height: 1, background: INK, transform: `scaleX(${ease(frame, s + 26, 50, inOut)})`, transformOrigin: "left" }} />
        </div>
        <div style={{ fontSize: 12, letterSpacing: 0.4, color: MUTED }}>Taking you to Explore…</div>
      </div>
      <div style={{ position: "absolute", top: 772, left: 0, right: 0, textAlign: "center", fontSize: 11, lineHeight: 1.6, color: MUTED, ...rise(frame, s + 14, 6) }}>
        In development · Starting in Adelaide · Adults 18+
      </div>
    </AbsoluteFill>
  );
};

// iOS-style push: the new screen slides in from the right while the one underneath drifts left
const Stack = () => {
  const frame = useCurrentFrame();
  const pushed = [
    { at: SIGNUP_TAP, back: SIGNUP_BACK, Screen: SignUp },
    { at: FORGOT_TAP, back: FORGOT_BACK, Screen: Forgot },
  ];
  const shift = pushed.reduce((x, p) => x + ease(frame, p.at, 20) - ease(frame, p.back, 20), 0);
  const welcome = ease(frame, WELCOME, 20);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ transform: `translateX(${-0.3 * SCREEN_W * (shift + welcome)}px)` }}>
        <SignIn />
      </AbsoluteFill>
      {pushed.map(({ at, back, Screen }) => {
        const x = 1 - ease(frame, at, 20) + ease(frame, back, 20);
        if (frame < at || x >= 1) return null;
        return (
          <AbsoluteFill key={at} style={{ transform: `translateX(${x * SCREEN_W}px)`, boxShadow: "-12px 0 30px rgba(0,0,0,0.08)" }}>
            <Screen />
          </AbsoluteFill>
        );
      })}
      <PasskeySheet />
      {frame >= WELCOME && (
        <AbsoluteFill style={{ transform: `translateX(${(1 - welcome) * SCREEN_W}px)`, boxShadow: "-12px 0 30px rgba(0,0,0,0.08)" }}>
          <Welcome />
        </AbsoluteFill>
      )}
    </AbsoluteFill>
  );
};

export const SignInDemo = () => (
  <AbsoluteFill style={{ background: useBg() }}>
    <Hook lines={["Getting in", "should be easy."]} until={PHONE_IN} />
    <Phone inAt={PHONE_IN} concept="Design concept · planned sign-in">
      <Stack />
      <Tap x={250} y={SI.create + 10} at={SIGNUP_TAP} />
      <Tap x={200} y={SU.name + 28} at={NAME_TYPE - 6} />
      <Tap x={42} y={SU.adult + 12} at={ADULT_TAP} />
      <Tap x={270} y={SU.signIn + 10} at={SIGNUP_BACK} />
      <Tap x={320} y={SI.forgot + 9} at={FORGOT_TAP} />
      <Tap x={200} y={282 + 28} at={FP_TYPE - 6} />
      <Tap x={195} y={FP.send + 27} at={SEND_TAP} />
      <Tap x={195} y={FP.send + 27} at={FORGOT_BACK} />
      <Tap x={195} y={SI.passkey + 27} at={PASSKEY_TAP} />
      <Tap x={195} y={SHEET_CONTINUE} at={CONTINUE_TAP} />
    </Phone>
    <Caption text="Sign in your way." from={PHONE_IN} to={SIGNUP_TAP} />
    <Caption text="New here? Make an account." from={SIGNUP_TAP} to={FORGOT_TAP} />
    <Caption text="Forgot it? Reset by email." from={FORGOT_TAP} to={PASSKEY_TAP} />
    <Caption text="Or skip the password." from={PASSKEY_TAP} to={OUTRO} />
    <EndCard at={OUTRO} tagline="Your kind of connection." />
  </AbsoluteFill>
);

// Voiceover lines in frames on this video's timeline; render.sh writes them to voiceover/<id>.srt
export const voiceover: Cue[] = [
  { from: 3, to: 52, text: "Getting in should be easy." },
  { from: PHONE_IN + 40, to: SIGNUP_TAP - 5, text: "Here's the planned Swoovr sign-in." },
  { from: SIGNUP_TAP + 8, to: SIGNUP_BACK - 10, text: "New here? Create an account. Adults 18 and over." },
  { from: FORGOT_TAP + 8, to: FORGOT_BACK, text: "Forgot your password? We'll email you a reset link." },
  { from: PASSKEY_TAP + 5, to: WELCOME + 50, text: "Or skip the password, and sign in with a passkey." },
  { from: OUTRO + 13, to: OUTRO + 139, text: "Swoovr. Your kind of connection. Now in development, starting in Adelaide." },
];
