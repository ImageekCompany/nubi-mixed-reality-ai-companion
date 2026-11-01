"""Nubi's animation clips, authored as frame -> PoseSpec functions at 30 fps.

Principles used throughout:
  * slow-in/slow-out: every keyed segment is smoothstep-eased (see `track`);
  * anticipation: one-shots dip/wind-up the opposite way before the main action;
  * overlap/follow-through: children sample the same curve a few frames late (`lag`), and the sprout
    always trails the head;
  * planted feet: legs are solved with IK every frame, so hips can bob/lean without foot sliding.
Root is never animated (root motion off); Walk is in place.
"""

from dataclasses import dataclass
from math import pi, sin, cos
from typing import Callable

from .posing import PoseSpec, mirror_euler

ZERO = (0.0, 0.0, 0.0)
TAU = 2 * pi


@dataclass
class Clip:
    name: str
    length: int          # last frame (inclusive); loops repeat frame 0 at `length`
    loop: bool
    pose: Callable[[float], PoseSpec]


# ------------------------------------------------------------ helpers ----

def _ease(t):
    return t * t * (3.0 - 2.0 * t)


def track(frame, keys, lag=0.0):
    """Piecewise eased interpolation of (frame, value) keys; value is a float or 3-tuple."""
    f = frame - lag
    if f <= keys[0][0]:
        return keys[0][1]
    for (f0, v0), (f1, v1) in zip(keys, keys[1:]):
        if f <= f1:
            t = _ease((f - f0) / (f1 - f0))
            if isinstance(v0, tuple):
                return tuple(a + (b - a) * t for a, b in zip(v0, v1))
            return v0 + (v1 - v0) * t
    return keys[-1][1]


def wave(t, amp, phase=0.0, harmonic=1):
    """Loop-safe sine: t in [0, 1) over the clip."""
    return amp * sin(TAU * (harmonic * t - phase))


def add(*vs):
    return tuple(sum(c) for c in zip(*vs))


def scale(v, s):
    return tuple(c * s for c in v)


def arms(spec, side, upper=ZERO, lower=ZERO, hand=ZERO):
    """Arm rotations authored in LEFT-arm convention, mirrored for the right arm."""
    m = (lambda r: r) if side == "L" else mirror_euler
    spec.rot[f"UpperArm_{side}"] = m(upper)
    spec.rot[f"LowerArm_{side}"] = m(lower)
    spec.rot[f"Hand_{side}"] = m(hand)


def sprout_follow(spec, frame, keys_fn, lag1=3.0, lag2=6.0, gain1=1.0, gain2=1.4):
    """Sprout trails a driver curve (usually the head's motion, inverted) for follow-through."""
    spec.rot["Sprout_1"] = scale(keys_fn(frame - lag1), gain1)
    spec.rot["Sprout_2"] = scale(keys_fn(frame - lag2), gain2)


# --------------------------------------------------------------- loops ----

def idle(f):
    L = 100.0
    t = f / L
    s = PoseSpec()
    s.loc["Hips"] = (wave(t, 0.005), 0.0, -0.002 - wave(t, 0.002, 0.25, 2))
    s.rot["Hips"] = (0.0, wave(t, 1.2, 0.05), 0.0)
    s.rot["Spine"] = (wave(t, -1.2), wave(t, -1.0, 0.1), 0.0)
    s.rot["Chest"] = (wave(t, -1.0, 0.05), wave(t, -0.8, 0.12), wave(t, 0.8, 0.2))
    s.rot["Head"] = (wave(t, 1.5, 0.2), wave(t, 2.5, 0.15), wave(t, 2.0, 0.3))
    for side, ph in (("L", 0.0), ("R", 0.12)):
        arms(s, side,
             upper=(wave(t, 1.5, 0.15 + ph), 2.0 + wave(t, -2.0, 0.1 + ph), 0.0),
             lower=(-6.0 + wave(t, -2.5, 0.2 + ph), 0.0, 0.0),
             hand=(wave(t, -3.0, 0.3 + ph), 0.0, 0.0))
    s.rot["Sprout_1"] = (wave(t, 3.0, 0.3), wave(t, -4.0, 0.25), 0.0)
    s.rot["Sprout_2"] = (wave(t, 4.0, 0.4), wave(t, -6.0, 0.35), 0.0)
    return s


# In-place gait. A stance foot slides back WALK_STRIDE metres in WALK_STANCE of a WALK_LENGTH-frame
# cycle, so 1x playback matches WALK_SPEED_MPS of root travel. The legs are only 12.5 cm long and
# straight at rest, so the stride comes from three sources: a deeper crouch, pelvis yaw (each hip
# swings ~3 cm fore/aft) and a high toddler cadence (3 steps/s).
WALK_LENGTH = 20
WALK_STRIDE = 0.180
WALK_STANCE = 0.55
WALK_LIFT = 0.045
WALK_SPEED_MPS = WALK_STRIDE / (WALK_STANCE * WALK_LENGTH / 30.0)   # 0.49 m/s
WALK_YAW = 14.0
WALK_CROUCH = 0.027
WALK_BOB = 0.011


def _foot_phase(p, stride, stance, lift_height):
    """In-place gait for one foot. p in [0,1): contact at 0. Returns (y offset, lift, pitch deg)."""
    half = stride * 0.5
    if p < stance:
        q = p / stance
        y = -half + stride * q                   # planted foot slides back under the moving body
        heel = max(0.0, (q - 0.8) / 0.2)          # heel peels off at the end of stance
        return y, 0.012 * heel, -12.0 * heel
    q = (p - stance) / (1.0 - stance)
    y = half - stride * _ease(q)
    lift = lift_height * sin(pi * q) ** 1.2
    pitch = -12.0 * (1 - q) * (1 - q) + 14.0 * sin(pi * q) * q  # toe-off -> toe-up before contact
    return y, lift, pitch


def walk(f):
    t = f / WALK_LENGTH
    s = PoseSpec()
    for side, offset in (("L", 0.0), ("R", 0.5)):
        y, lift, pitch = _foot_phase((t + offset) % 1.0, WALK_STRIDE, WALK_STANCE, WALK_LIFT)
        s.foot[side] = ((0.0, y, lift), (pitch, 0.0, 0.0))
    # Bouncy waddle: down just after each contact, up at the passing pose; the blob rolls over the
    # stance foot and the pelvis yaws with the legs while the chest counter-twists to keep the face forward.
    bob = -WALK_CROUCH - WALK_BOB * cos(TAU * (2 * t - 0.06))
    yaw = -WALK_YAW * cos(TAU * (t - 0.04))            # left hip forward at left contact (t = 0)
    roll = 6.0 * sin(TAU * (t - 0.04))                 # tilt over the stance foot (L for t in 0..0.5)
    s.loc["Hips"] = (0.014 * sin(TAU * (t - 0.06)), 0.0, bob)
    s.rot["Hips"] = (5.0, roll, yaw)
    s.rot["Spine"] = (1.0 + 1.5 * cos(TAU * (2 * t - 0.15)), -0.35 * roll, -0.40 * yaw)
    s.rot["Chest"] = (1.0 + 1.5 * cos(TAU * (2 * t - 0.22)), -0.25 * roll, -0.35 * yaw)
    s.rot["Head"] = (-4.0 + 2.5 * cos(TAU * (2 * t - 0.30)), -0.30 * roll, -0.10 * yaw)
    swing = 25.0
    arms(s, "L", upper=(swing * cos(TAU * (t - 0.06)), 6.0, 0.0),
         lower=(-14.0 - 10.0 * cos(TAU * (t - 0.14)), 0.0, 0.0),
         hand=(-8.0 * cos(TAU * (t - 0.2)), 0.0, 0.0))
    arms(s, "R", upper=(-swing * cos(TAU * (t - 0.06)), 6.0, 0.0),
         lower=(-14.0 + 10.0 * cos(TAU * (t - 0.14)), 0.0, 0.0),
         hand=(8.0 * cos(TAU * (t - 0.2)), 0.0, 0.0))
    # Sprout streams back from the forward motion and bounces a few frames behind the bob.
    s.rot["Sprout_1"] = (-10.0 + 7.0 * cos(TAU * (2 * t - 0.22)), 0.5 * roll, 0.0)
    s.rot["Sprout_2"] = (-14.0 + 11.0 * cos(TAU * (2 * t - 0.34)), 0.8 * roll, 0.0)
    return s


# Catch-up gait: two-footed bunny hops. Feet are planted for HOP_CONTACT frames of a HOP_LENGTH-frame
# cycle and slide back HOP_STRIDE metres meanwhile, so 1x matches HOP_SPEED_MPS of root travel.
HOP_LENGTH = 16
HOP_CONTACT = 5
HOP_STRIDE = 0.150
HOP_SPEED_MPS = HOP_STRIDE / (HOP_CONTACT / 30.0)                   # 0.90 m/s
HOP_HEIGHT = 0.090
HOP_CROUCH = 0.034     # hips this low at touch-down/take-off: the 12.5 cm legs need it to reach +-7.5 cm


def hop(f):
    s = PoseSpec()
    c = float(HOP_CONTACT)
    air = HOP_LENGTH - c
    half = HOP_STRIDE * 0.5
    if f <= c:                                         # planted: squash, feet slide back under the body
        q = f / c
        y, lift, pitch = -half + HOP_STRIDE * q, 0.0, -14.0 * max(0.0, (q - 0.7) / 0.3)
        hz = -HOP_CROUCH - 0.014 * sin(pi * q)
    else:                                              # airborne: feet tuck and swing forward
        q = (f - c) / air
        y = half - HOP_STRIDE * _ease(q)
        hz = -HOP_CROUCH + (HOP_HEIGHT + HOP_CROUCH) * sin(pi * q) ** 0.8
        lift = (hz + HOP_CROUCH) * 0.75
        pitch = -14.0 * (1 - q) ** 2 + 10.0 * sin(pi * q) * q
    for side in "LR":
        s.foot[side] = ((0.0, y, lift), (pitch, 0.0, 0.0))
    t = f / HOP_LENGTH
    s.loc["Hips"] = (0.0, 0.0, hz)
    stretch = max(0.0, hz + HOP_CROUCH) / (HOP_HEIGHT + HOP_CROUCH)   # 0 on the ground, 1 at the apex
    s.rot["Hips"] = (8.0 - 4.0 * stretch, 0.0, 0.0)
    s.rot["Spine"] = (3.0 * cos(TAU * (t - 0.10)), 0.0, 0.0)
    s.rot["Chest"] = (3.0 * cos(TAU * (t - 0.16)), 0.0, 0.0)
    s.rot["Head"] = (-5.0 + 4.0 * cos(TAU * (t - 0.24)), 0.0, 0.0)
    for side in "LR":
        arms(s, side, upper=(-10.0 - 22.0 * cos(TAU * (t - 0.12)), -10.0 - 22.0 * stretch, 0.0),
             lower=(-16.0 - 12.0 * cos(TAU * (t - 0.20)), 0.0, 0.0),
             hand=(-10.0 * cos(TAU * (t - 0.26)), 0.0, 0.0))
    s.rot["Sprout_1"] = (-12.0 + 12.0 * cos(TAU * (t - 0.28)), 0.0, 0.0)
    s.rot["Sprout_2"] = (-16.0 + 18.0 * cos(TAU * (t - 0.40)), 0.0, 0.0)
    return s


def talk(f):
    L = 60.0
    t = f / L
    s = PoseSpec()
    gesture_l = lambda x: (0.5 - 0.5 * cos(TAU * x)) ** 1.5          # left hand peaks mid-clip
    gesture_r = lambda x: (0.5 - 0.5 * cos(TAU * (x - 0.5))) ** 1.5  # right hand peaks at the seam

    def lagged(g_fn, lag):
        return g_fn((t - lag / L) % 1.0)

    g_l, g_r = gesture_l(t), gesture_r(t)
    for side, g, gfn in (("L", g_l, gesture_l), ("R", g_r, gesture_r)):
        g2, g3 = lagged(gfn, 3), lagged(gfn, 6)
        arms(s, side,
             upper=(-10.0 - 28.0 * g, -6.0 - 14.0 * g, -4.0 * g),
             lower=(-18.0 - 38.0 * g2, 0.0, 6.0 * g2),
             hand=(-14.0 * g3, 0.0, 10.0 * g3))
    s.loc["Hips"] = (0.0, 0.0, -0.002 - 0.003 * (0.5 - 0.5 * cos(TAU * 2 * t)))
    s.rot["Spine"] = (1.5 + wave(t, 1.5, 0.1, 2), 0.0, 2.0 * (g_l - g_r))
    s.rot["Chest"] = (1.0 + wave(t, 1.5, 0.15, 2), wave(t, 1.5, 0.0), 3.0 * (g_l - g_r))
    s.rot["Head"] = (2.0 + wave(t, 3.0, 0.2, 2), wave(t, 4.0, 0.15), 3.0 * (lagged(gesture_l, 4) - lagged(gesture_r, 4)))
    s.rot["Sprout_1"] = (wave(t, -4.0, 0.3, 2), wave(t, -5.0, 0.3), 0.0)
    s.rot["Sprout_2"] = (wave(t, -6.0, 0.4, 2), wave(t, -7.0, 0.4), 0.0)
    return s


def listen(f):
    L = 90.0
    t = f / L
    s = PoseSpec()
    nod = max(0.0, sin(TAU * (t - 0.55))) ** 3      # one soft acknowledging nod per loop
    s.loc["Hips"] = (0.0, 0.0, -0.004 + wave(t, 0.0015))
    s.rot["Hips"] = (7.0, 0.0, 0.0)  # lean in from the hips: the blob tilts rigidly, no belly crease
    s.rot["Spine"] = (1.5 + wave(t, -0.8), 0.0, 0.0)
    s.rot["Chest"] = (1.0 + wave(t, -0.8, 0.05), 1.5, 0.0)
    s.rot["Neck"] = (1.0, 3.0, 0.0)
    s.rot["Head"] = (2.0 + 5.0 * nod + wave(t, 1.0, 0.2), 11.0 + wave(t, 1.5, 0.3), 5.0)
    for side, ph in (("L", 0.0), ("R", 0.1)):
        arms(s, side, upper=(-12.0 + wave(t, 1.0, ph), 8.0, 0.0),
             lower=(-28.0 + wave(t, -2.0, 0.1 + ph), 0.0, 0.0),
             hand=(-8.0, 0.0, 0.0))
    s.rot["Sprout_1"] = (4.0 + 5.0 * max(0.0, sin(TAU * (t - 0.6))) ** 3, -8.0 + wave(t, -2.0, 0.4), 0.0)
    s.rot["Sprout_2"] = (5.0 + 7.0 * max(0.0, sin(TAU * (t - 0.64))) ** 3, -10.0 + wave(t, -3.0, 0.5), 0.0)
    return s


# Right mitten tip on the side of the lower face, in REST character space; it rides the posed Head
# (PoseSpec.hand_ik_space), so the head chain can tilt into the hand and the contact holds. The axis
# point sits ~1.8 cm off the skin (the mitten's half-thickness) minus ~5 mm, so the palm presses in.
THINK_CHEEK_R = (-0.205, -0.245, 0.565)
# Elbow drops forward and down, so the forearm rises along the face and the mitten lies on the cheek
# instead of stabbing into it from the side.
THINK_ELBOW_POLE_R = (-0.35, -0.75, -0.55)
THINK_EYES = (-7.0, 0.0, 9.0)              # look up and away from the hand, inside the eye-bone range


def think(f):
    L = 90.0
    t = f / L
    s = PoseSpec()
    tap = max(0.0, sin(TAU * 2 * (t - 0.1))) ** 4   # two thoughtful taps per loop
    # The head chain tilts ~14 deg into the raised hand and turns the right cheek back towards it, so
    # cheek and mitten meet halfway; the short arm cannot travel far enough on its own.
    s.loc["Hips"] = (0.003, 0.0, -0.006)
    s.rot["Hips"] = (0.0, -4.0 + wave(t, 0.8, 0.1), 0.0)
    s.rot["Spine"] = (-1.0 + wave(t, -0.6), 0.0, 0.0)
    s.rot["Chest"] = (-1.0, -1.0, 3.0)
    s.rot["Neck"] = (0.0, -3.0, -5.0)
    # Looks up and towards the raised hand, with a slow searching drift.
    s.rot["Head"] = (-8.0 + wave(t, 1.5, 0.1), -6.0 + wave(t, 1.5, 0.2), -13.0 + wave(t, 3.0, 0.0))
    eyes = add(THINK_EYES, (wave(t, 1.0, 0.3), 0.0, wave(t, 3.0, 0.05)))
    s.rot["Eye_L"] = eyes
    s.rot["Eye_R"] = eyes
    s.hand_ik["R"] = add(THINK_CHEEK_R, (-0.010 * tap, -0.006 * tap, -0.004 * tap))  # taps lift off the cheek
    s.hand_ik_space["R"] = "Head"
    s.hand_ik_pole["R"] = THINK_ELBOW_POLE_R
    arms(s, "L", upper=(-10.0, 8.0, 0.0), lower=(-30.0, 0.0, 6.0), hand=(-8.0, 0.0, 0.0))
    s.rot["Sprout_1"] = (-5.0 + wave(t, 3.0, 0.15), 12.0 + wave(t, -4.0, 0.25), 0.0)
    s.rot["Sprout_2"] = (-6.0 + wave(t, 4.0, 0.25), 16.0 + wave(t, -6.0, 0.35), 0.0)
    return s


def sad(f):
    L = 90.0
    t = f / L
    s = PoseSpec()
    sigh = max(0.0, sin(TAU * (t - 0.35))) ** 2       # one slow sigh per loop
    s.loc["Hips"] = (0.0, 0.0, -0.012 + 0.004 * sigh)
    s.rot["Hips"] = (8.0, 0.0, 0.0)
    s.rot["Spine"] = (3.0 - 2.0 * sigh, 0.0, 0.0)
    s.rot["Chest"] = (2.5 - 2.0 * sigh, 0.0, 0.0)
    s.rot["Neck"] = (2.0, 0.0, 0.0)
    s.rot["Head"] = (9.0 - 3.0 * sigh + wave(t, 1.0, 0.2), -6.0, wave(t, 1.5, 0.1))
    for side, ph in (("L", 0.0), ("R", 0.08)):
        arms(s, side, upper=(-8.0 + wave(t, 1.0, ph), 9.0, 0.0), lower=(-14.0, 0.0, 0.0), hand=(-6.0, 0.0, 0.0))
    s.rot["Sprout_1"] = (20.0 - 6.0 * sigh, wave(t, -2.0, 0.3), 0.0)
    s.rot["Sprout_2"] = (24.0 - 8.0 * sigh, wave(t, -3.0, 0.4), 0.0)
    return s


# ----------------------------------------------------------- one-shots ----

def wave_hello(f):
    s = PoseSpec()
    up = [(0, ZERO), (6, (4.0, 4.0, 0.0)), (14, (-28.0, -104.0, 0.0)), (17, (-26.0, -97.0, 0.0)),
          (44, (-26.0, -97.0, 0.0)), (54, (2.0, 3.0, 0.0)), (60, ZERO)]
    flick = [(0, ZERO), (14, (0.0, -15.0, 0.0)), (20, (0.0, 30.0, 0.0)), (26, (0.0, -15.0, 0.0)),
             (32, (0.0, 30.0, 0.0)), (38, (0.0, -15.0, 0.0)), (44, (0.0, 10.0, 0.0)), (54, ZERO)]
    arms(s, "R", upper=track(f, up), lower=add(track(f, flick), track(f, [(0, ZERO), (14, (-10.0, 0, 0)),
                                                                           (46, (-10.0, 0, 0)), (56, ZERO)])),
         hand=scale(track(f, flick, lag=3), 0.6))
    arms(s, "L", upper=track(f, [(0, ZERO), (10, (-4.0, 4.0, 0.0)), (48, (-4.0, 4.0, 0.0)), (60, ZERO)]),
         lower=track(f, [(0, ZERO), (10, (-10.0, 0, 0)), (48, (-10.0, 0, 0)), (60, ZERO)]))
    body = [(0, ZERO), (6, (3.0, 0.0, 0.0)), (14, (-2.0, 4.0, 3.0)), (46, (-2.0, 4.0, 3.0)), (60, ZERO)]
    s.rot["Chest"] = track(f, body)
    s.rot["Spine"] = scale(track(f, body, lag=1), 0.6)
    s.loc["Hips"] = track(f, [(0, ZERO), (6, (0.0, 0.0, -0.010)), (14, (0.0, 0.0, 0.004)),
                              (18, ZERO), (60, ZERO)])
    # Head tilts away from the raised arm (towards it would push the cheek into the upper arm).
    head = [(0, ZERO), (7, (4.0, 0.0, 0.0)), (16, (-5.0, 6.0, -6.0)), (46, (-5.0, 6.0, -6.0)), (60, ZERO)]
    s.rot["Head"] = track(f, head, lag=2)
    sprout_follow(s, f, lambda x: scale(track(x, head), -1.2))
    return s


POINT_DIR_R = (-0.20, -0.90, 0.38)
REST_ARM_DIR_R = (-0.62, -0.10, -0.78)


def point(f):
    s = PoseSpec()
    # 0 = rest direction, 1 = pointing; < 0 winds the arm back, > 1 overshoots before settling.
    reach = track(f, [(0, 0.0), (6, -0.25), (13, 1.10), (17, 0.96), (21, 1.0), (45, 1.0)])
    s.aim["UpperArm_R"] = tuple(a + (b - a) * reach for a, b in zip(REST_ARM_DIR_R, POINT_DIR_R))
    arms(s, "R", lower=track(f, [(0, ZERO), (6, (-14.0, 0, 0)), (12, ZERO)]),
         hand=track(f, [(0, ZERO), (13, ZERO), (17, (8.0, 0, 0)), (22, ZERO)]))
    s.rot["UpperArm_R"] = ZERO
    arms(s, "L", upper=track(f, [(0, ZERO), (12, (8.0, -6.0, 0.0)), (45, (8.0, -6.0, 0.0))], lag=2),
         lower=track(f, [(0, ZERO), (12, (-12.0, 0, 0)), (45, (-12.0, 0, 0))], lag=3))
    # The torso turns to its left to bring the short right arm forward of the belly.
    body = [(0, ZERO), (6, (-2.0, 0.0, -6.0)), (13, (5.0, 0.0, 14.0)), (17, (4.0, 0.0, 12.0)), (45, (4.0, 0.0, 12.0))]
    s.rot["Hips"] = scale(track(f, body), 0.6)
    s.rot["Spine"] = scale(track(f, body, lag=1), 0.2)
    s.rot["Chest"] = scale(track(f, body, lag=1.5), 0.25)
    s.loc["Hips"] = track(f, [(0, ZERO), (6, (0.0, 0.006, -0.006)), (13, (0.0, -0.008, -0.004)),
                              (45, (0.0, -0.008, -0.004))])
    head = [(0, ZERO), (7, (2.0, 0.0, 3.0)), (15, (2.0, -3.0, -16.0)), (45, (2.0, -3.0, -16.0))]
    s.rot["Head"] = track(f, head, lag=2)
    sprout_follow(s, f, lambda x: scale(track(x, body), -1.0))
    return s


def happy(f):
    s = PoseSpec()
    hz = track(f, [(0, 0.0), (6, -0.032), (10, 0.010), (15, 0.080), (19, 0.030), (22, -0.026),
                   (27, 0.006), (32, 0.0), (36, 0.0)])
    s.loc["Hips"] = (0.0, 0.0, hz)
    airborne = max(0.0, hz - 0.0)
    toe = track(f, [(0, 0.0), (9, 0.0), (14, -14.0), (20, 0.0), (36, 0.0)])
    for side in "LR":
        s.foot[side] = ((0.0, 0.0, airborne), (toe, 0.0, 0.0))
    crouch = [(0, ZERO), (6, (7.0, 0.0, 0.0)), (11, (-4.0, 0.0, 0.0)), (15, (-6.0, 0.0, 0.0)),
              (22, (6.0, 0.0, 0.0)), (28, (-1.0, 0.0, 0.0)), (34, ZERO)]
    s.rot["Spine"] = track(f, crouch)
    s.rot["Chest"] = track(f, crouch, lag=1.5)
    s.rot["Head"] = add(scale(track(f, crouch, lag=3), -0.8), track(f, [(0, ZERO), (12, (-6.0, 0, 0)),
                                                                          (26, (-6.0, 0, 0)), (34, ZERO)]))
    arm_up = [(0, ZERO), (6, (14.0, 6.0, 0.0)), (13, (-20.0, -78.0, 0.0)), (17, (-22.0, -88.0, 0.0)),
              (23, (-10.0, -40.0, 0.0)), (30, (2.0, 4.0, 0.0)), (36, ZERO)]
    for side in "LR":
        arms(s, side, upper=track(f, arm_up, lag=1.0),
             lower=scale(track(f, arm_up, lag=3.0), 0.35), hand=scale(track(f, arm_up, lag=4.0), 0.15))
    bounce = [(0, ZERO), (6, (-10.0, 0, 0)), (12, (18.0, 0, 0)), (16, (24.0, 0, 0)), (22, (-22.0, 0, 0)),
              (27, (10.0, 0, 0)), (31, (-4.0, 0, 0)), (36, ZERO)]
    sprout_follow(s, f, lambda x: track(x, bounce), lag1=1.0, lag2=3.0)
    return s


def squash(k):
    """Volume-keeping bone-local squash (k > 0) / stretch (k < 0) along the bone."""
    return (1.0 + 0.5 * k, 1.0 - k, 1.0 + 0.5 * k)


# The "head" is the whole upper blob, so a head-only rotation barely reads: the nod/shake is spread
# down the chain (Hips -> Head) and the body squashes into the downbeats.
NOD_SHARE = {"Hips": (0.20, 1.0), "Chest": (0.15, 2.0), "Neck": (0.35, 1.0), "Head": (0.75, 0.0)}  # (gain, lag)
NOD_SQUASH = 0.05


def nod(f):
    s = PoseSpec()
    head = [(0, 0.0), (4, -6.0), (9, 26.0), (14, 0.0), (18, 18.0), (23, -2.0), (27, 1.0), (30, 0.0)]
    for bone, (gain, lag) in NOD_SHARE.items():
        s.rot[bone] = (gain * track(f, head, lag), 0.0, 0.0)
    down = max(0.0, track(f, head, 1.0)) / 26.0
    s.scl["Spine"] = squash(NOD_SQUASH * down)
    s.loc["Hips"] = (0.0, 0.0, -0.008 * down)
    sprout_follow(s, f, lambda x: (-1.3 * track(x, head), 0.0, 0.0), lag1=2.0, lag2=4.0)
    return s


SHAKE_SHARE = {"Hips": (0.15, 1.0), "Chest": (0.12, 2.0), "Neck": (0.30, 1.0), "Head": (0.80, 0.0)}
SHAKE_SQUASH = 0.025


def shake_head(f):
    s = PoseSpec()
    yaw = [(0, 0.0), (3, 5.0), (7, -28.0), (13, 28.0), (19, -20.0), (25, 11.0), (31, -3.0), (36, 0.0)]
    pitch = track(f, [(0, 0.0), (3, 2.0), (7, 4.0), (31, 4.0), (36, 0.0)])
    for bone, (gain, lag) in SHAKE_SHARE.items():
        y = track(f, yaw, lag)
        s.rot[bone] = (pitch if bone == "Head" else 0.0, -0.12 * y if bone == "Head" else 0.0, gain * y)
    s.scl["Spine"] = squash(SHAKE_SQUASH * abs(track(f, yaw, 1.0)) / 28.0)
    # Sprout swings sideways (tilt) opposite to the head turn, trailing it.
    sprout_follow(s, f, lambda x: (0.0, track(x, yaw) * 0.9, 0.0), lag1=2.0, lag2=4.0)
    return s


def surprised(f):
    s = PoseSpec()
    jolt = track(f, [(0, 0.0), (3, -0.4), (8, 1.1), (11, 1.0), (30, 1.0), (40, 0.0)])
    s.loc["Hips"] = (0.0, 0.010 * jolt, track(f, [(0, 0.0), (3, -0.010), (8, 0.022), (12, 0.004),
                                                  (30, 0.004), (40, 0.0)]))
    airborne = max(0.0, s.loc["Hips"][2] - 0.012)
    for side in "LR":
        s.foot[side] = ((0.0, 0.0, airborne), (0.0, 0.0, 0.0))
    s.rot["Spine"] = (-6.0 * jolt, 0.0, 0.0)
    s.rot["Chest"] = (-5.0 * track(f, [(0, 0.0), (3, -0.4), (9, 1.1), (12, 1.0), (30, 1.0), (40, 0.0)]), 0.0, 0.0)
    s.rot["Head"] = (-9.0 * track(f, [(0, 0.0), (3, -0.4), (10, 1.1), (13, 1.0), (30, 1.0), (40, 0.0)]), 0.0, 0.0)
    for side in "LR":
        j = track(f, [(0, 0.0), (3, -0.1), (9, 1.1), (12, 1.0), (30, 1.0), (40, 0.0)])
        j2 = track(f, [(0, 0.0), (3, -0.3), (11, 1.15), (14, 1.0), (30, 1.0), (40, 0.0)])
        arms(s, side, upper=(-28.0 * j, -52.0 * j, 0.0), lower=(-40.0 * j2, 0.0, 0.0), hand=(-15.0 * j2, 0.0, 0.0))
    spring = [(0, ZERO), (4, (6.0, 0, 0)), (8, (-26.0, 0, 0)), (12, (14.0, 0, 0)), (16, (-6.0, 0, 0)),
              (20, (-2.0, 0, 0)), (34, (-2.0, 0, 0)), (40, ZERO)]
    sprout_follow(s, f, lambda x: track(x, spring), lag1=0.0, lag2=2.0)
    return s


CLIPS = (
    Clip("Idle", 100, True, idle),
    Clip("Walk", WALK_LENGTH, True, walk),
    Clip("Hop", HOP_LENGTH, True, hop),
    Clip("Talk", 60, True, talk),
    Clip("Listen", 90, True, listen),
    Clip("Think", 90, True, think),
    Clip("Wave", 60, False, wave_hello),
    Clip("Point", 45, False, point),
    Clip("Happy", 36, False, happy),
    Clip("Nod", 30, False, nod),
    Clip("ShakeHead", 36, False, shake_head),
    Clip("Sad", 90, True, sad),
    Clip("Surprised", 40, False, surprised),
)
