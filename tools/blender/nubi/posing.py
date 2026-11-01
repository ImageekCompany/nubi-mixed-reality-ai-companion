"""Character-space posing: FK, two-bone IK and conversion to Blender pose-bone channels.

Clips are authored as functions frame -> PoseSpec in *character space* (see config.py axes), so an
animator-style "lean forward 5 degrees" is (5, 0, 0) on any bone regardless of its roll. A bone's
character-space delta is applied in its parent's posed frame, which is exactly how Blender composes
pose rotations once converted with q_local = R_rest^-1 * q * R_rest.

Euler conventions (degrees, XYZ):
  spine/neck/head/sprout: +X bends forward / looks down, +Y tilts to Nubi's left, +Z turns to Nubi's left.
  arms (authored for the LEFT arm, mirrored for the right): -X swings forward, -Y raises sideways,
  +Y lowers towards the body, +Z swings back horizontally.
"""

from dataclasses import dataclass, field
from math import acos, cos, sin, radians

from mathutils import Euler, Quaternion, Vector

# Knees bend forward; elbows point back and out.
KNEE_POLE = Vector((0, -1, 0))
ELBOW_POLE = {"L": Vector((0.9, 0.35, -0.25)).normalized(), "R": Vector((-0.9, 0.35, -0.25)).normalized()}
IK_REACH_EPS = 1e-4


@dataclass
class PoseSpec:
    rot: dict = field(default_factory=dict)        # bone -> euler degrees (character space)
    loc: dict = field(default_factory=dict)        # bone -> metres (character space)
    aim: dict = field(default_factory=dict)        # bone -> absolute character-space direction
    foot: dict = field(default_factory=dict)       # side -> (ankle offset from rest, foot euler degrees)
    hand_ik: dict = field(default_factory=dict)    # side -> hand-tip target (character space)
    # side -> bone: the hand_ik target is given in REST character space and rides that bone's posed
    # transform, so a hand stays on the cheek however the head chain above it is posed.
    hand_ik_space: dict = field(default_factory=dict)
    hand_ik_pole: dict = field(default_factory=dict)  # side -> elbow pole override (character space)
    # bone -> BONE-LOCAL scale (x, y, z), y along the bone. Inherited by children. FK/IK below ignore it,
    # so only use it where no IK target hangs off the scaled bone (squash on Spine in Nod/ShakeHead).
    scl: dict = field(default_factory=dict)


def euler_q(deg):
    return Euler((radians(deg[0]), radians(deg[1]), radians(deg[2])), "XYZ").to_quaternion()


def mirror_euler(deg):
    return (deg[0], -deg[1], -deg[2])


def mirror_vec(v):
    return (-v[0], v[1], v[2])


class RigModel:
    """Rest data pulled once from the armature; evaluates PoseSpecs into per-bone local channels."""

    def __init__(self, arm_obj):
        self.names = []
        self.parent = {}
        self.head = {}
        self.tail = {}
        self.rest_q = {}
        for b in arm_obj.data.bones:  # parents always precede children in this collection
            self.names.append(b.name)
            self.parent[b.name] = b.parent.name if b.parent else None
            self.head[b.name] = b.head_local.copy()
            self.tail[b.name] = b.tail_local.copy()
            self.rest_q[b.name] = b.matrix_local.to_quaternion()
        self._order_parents_first()

    def _order_parents_first(self):
        ordered, seen = [], set()

        def visit(n):
            if n in seen:
                return
            if self.parent[n]:
                visit(self.parent[n])
            seen.add(n)
            ordered.append(n)
        for n in self.names:
            visit(n)
        self.names = ordered

    def rest_dir(self, bone):
        return (self.tail[bone] - self.head[bone]).normalized()

    def length(self, bone):
        return (self.tail[bone] - self.head[bone]).length

    # ---------------------------------------------------------------- FK ----
    def fk(self, q_delta, t_delta):
        """World (character-space) rotation and head position per bone."""
        Q, P = {}, {}
        for n in self.names:
            p = self.parent[n]
            q = q_delta.get(n, Quaternion())
            t = Vector(t_delta.get(n, (0, 0, 0)))
            if p is None:
                Q[n] = q
                P[n] = self.head[n] + t
            else:
                Q[n] = Q[p] @ q
                P[n] = P[p] + Q[p] @ (self.head[n] - self.head[p] + t)
        return Q, P

    # ---------------------------------------------------------------- IK ----
    @staticmethod
    def two_bone(root, target, l1, l2, pole):
        d = target - root
        dist = min(max(d.length, IK_REACH_EPS), l1 + l2 - IK_REACH_EPS)
        dn = d.normalized()
        cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
        a = acos(min(max(cos_a, -1.0), 1.0))
        bend = pole - dn * pole.dot(dn)
        if bend.length < 1e-6:
            bend = Vector((0, 0, 1)).cross(dn)
        bend.normalize()
        mid = root + (dn * cos(a) + bend * sin(a)) * l1
        return mid, root + dn * dist

    def solve_chain(self, q, t, upper, lower, end, target, pole, end_world=None, end_rigid=False):
        """Fill q[upper], q[lower] (and q[end]) so the chain reaches `target`.

        end_rigid: the end bone continues the lower bone (arm + mitten), target is the end tip."""
        Q, P = self.fk(q, t)
        parent_q = Q[self.parent[upper]]
        root = P[upper]
        l1 = self.length(upper)
        l2 = self.length(lower) + (self.length(end) if end_rigid else 0.0)
        mid, reach = self.two_bone(root, Vector(target), l1, l2, pole)
        q_u = self.rest_dir(upper).rotation_difference(parent_q.inverted() @ (mid - root).normalized())
        q_upper_world = parent_q @ q_u
        q_l = self.rest_dir(lower).rotation_difference(q_upper_world.inverted() @ (reach - mid).normalized())
        q[upper], q[lower] = q_u, q_l
        if end_rigid:
            q[end] = Quaternion()
        elif end_world is not None:
            q[end] = (q_upper_world @ q_l).inverted() @ end_world

    # ------------------------------------------------------------ evaluate ----
    def evaluate(self, spec):
        """PoseSpec -> {bone: (local quaternion, local location, local scale)} for Blender pose channels."""
        q, t = {}, {}
        for bone, deg in spec.rot.items():
            q[bone] = euler_q(deg)
        for bone, off in spec.loc.items():
            t[bone] = Vector(off)

        for bone, direction in spec.aim.items():
            Q, _P = self.fk(q, t)
            parent_q = Q[self.parent[bone]]
            q[bone] = self.rest_dir(bone).rotation_difference(parent_q.inverted() @ Vector(direction).normalized()) \
                @ q.get(bone, Quaternion())

        for side in "LR":
            ankle_off, foot_deg = spec.foot.get(side, ((0, 0, 0), (0, 0, 0)))
            target = self.head[f"Foot_{side}"] + Vector(ankle_off)
            self.solve_chain(q, t, f"UpperLeg_{side}", f"LowerLeg_{side}", f"Foot_{side}",
                             target, KNEE_POLE, end_world=euler_q(foot_deg))

        for side, target in spec.hand_ik.items():
            space = spec.hand_ik_space.get(side)
            if space:
                Q, P = self.fk(q, t)
                target = P[space] + Q[space] @ (Vector(target) - self.head[space])
            self.solve_chain(q, t, f"UpperArm_{side}", f"LowerArm_{side}", f"Hand_{side}",
                             target, Vector(spec.hand_ik_pole.get(side, ELBOW_POLE[side])).normalized(),
                             end_rigid=True)

        out = {}
        for n in self.names:
            rq = self.rest_q[n]
            qd = q.get(n, Quaternion())
            local_q = rq.inverted() @ qd @ rq
            local_t = rq.inverted() @ t.get(n, Vector())
            out[n] = (local_q, local_t, Vector(spec.scl.get(n, (1.0, 1.0, 1.0))))
        return out
