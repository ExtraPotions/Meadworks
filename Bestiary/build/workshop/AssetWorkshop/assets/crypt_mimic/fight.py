"""The full fight, beat by beat, at 30 frames a second. It follows the design: the bite on opening always lands, the
lunge can be dodged but not blocked, the bite recharges for 4 seconds, and hits do normal damage at any time.

    the ambush -> it wakes and hops after you -> lunge 1: dodged, three hits in the recovery -> it turns and chases ->
    lunge 2: you try to block, get bitten and knocked down -> a heavy hit staggers it -> lunge 3: dodged, three hits ->
    two hits as it turns on you -> it dies and spills its loot
"""
from actors import Hud, Mimic, Player
from clips import AMBUSH, DEATH, IDLE, LUNGE, STAGGER
from poses import DORMANT

COOLDOWN = 4.0
MIMIC_HP, PLAYER_HP = 400, 100
AMBUSH_BITE, LUNGE_BITE, HIT, HEAVY_HIT = 22, 35, 45, 70
SNAP_AMBUSH, SNAP_LUNGE = 10, 31        # frames into each clip where the jaws close


def choreograph():
    mimic, hud = Mimic(), Hud(MIMIC_HP, PLAYER_HP)
    player = Player(mimic.ahead(4.2), mimic.pos)
    frame, ready = opening(mimic, player, hud)
    frame = chase(mimic, player, hud, frame, ready, None)
    frame, ready = dodged_lunge(mimic, player, hud, frame, to_its_right=True)
    frame = chase(mimic, player, hud, frame, ready, (-3.0, -6.8))
    frame, ready = blocked_lunge(mimic, player, hud, frame)
    frame = stagger(mimic, player, hud, frame, ready, (-0.6, -8.2))
    frame, ready = dodged_lunge(mimic, player, hud, frame, to_its_right=False)
    end = finish(mimic, player, hud, frame)
    return mimic, player, hud, end


def opening(mimic, player, hud):
    hud.caption(1, "A stone chest in a burial chamber")
    mimic.hold(1, 42, DORMANT)
    player.key(1)
    player.move(4, 38, mimic.ahead(1.35), face=mimic.pos)
    hud.prompt = [22, 41]
    hud.caption(36, "Press E to open it...")
    player.key(40, lean=12)
    player.key(44)
    end = mimic.play(AMBUSH, 42)
    bite = 42 + SNAP_AMBUSH
    hud.reveal = 44
    hud.flash(bite, "BITE!", "red")
    hud.hit_player(bite, AMBUSH_BITE)
    hud.caption(bite, "...and it bites. The ambush can't be avoided")
    player.move(bite, bite + 8, mimic.ahead(2.6), lean=-22)
    player.key(bite + 14)
    return end, hud.cooldown(bite, COOLDOWN)


def chase(mimic, player, hud, frame, ready, retreat_to):
    """While the bite recharges it hops after the player, who backs off (to `retreat_to`, or straight back)."""
    hud.caption(frame, "Its bite is recharging. It hops after you")
    if retreat_to is None:
        player.move(frame + 6, frame + 36, mimic.ahead(4.6), face=mimic.pos)
        frame = mimic.play(IDLE, frame)
    else:
        player.move(frame + 2, frame + 30, retreat_to, face=mimic.pos)
        frame = mimic.turn_hop(frame, retreat_to)
    frame = mimic.hop(frame, max(0, (ready - frame) // 16))
    return mimic.hold(frame, ready) if frame < ready else frame


def dodged_lunge(mimic, player, hud, frame, to_its_right):
    """Wind-up, a dodge roll to one side, the jaws snap on air, and three hits while it recovers."""
    side = 1.9 if to_its_right else -1.9
    fx, fy = mimic.facing()
    hud.caption(frame, "Wind-up: the lid gapes open. Get ready to dodge")
    player.move(frame + 14, frame + 26, (player.x + fy * side, player.y - fx * side), tilt=-35 if to_its_right else 35, crouch=0.55)
    player.key(frame + 30)
    hud.caption(frame + 14, "Dodge!")
    end = mimic.play(LUNGE, frame)
    snap = frame + SNAP_LUNGE
    hud.flash(snap, "DODGED", "blue")
    hud.caption(snap + 2, "It snaps on air and is stuck recovering. Hit it!")
    player.move(snap + 4, snap + 14, mimic.ahead(0.0, right=1.45 if to_its_right else -1.45), face=mimic.pos)
    for strike in (snap + 20, snap + 32, snap + 44):
        player.swing(strike)
        hud.hit_mimic(strike, HIT)
    return end, hud.cooldown(snap, COOLDOWN)


def blocked_lunge(mimic, player, hud, frame):
    """The player raises the sword to block; the bite lands anyway and knocks them flat."""
    hud.caption(frame, "It lunges again. This time you try to block")
    player.sword += [(frame + 4, 0.0), (frame + 12, 95.0), (frame + 30, 95.0)]
    hud.caption(frame + 16, "Blocking won't stop it. Only a dodge does")
    end = mimic.play(LUNGE, frame)
    snap = frame + SNAP_LUNGE
    hud.flash(snap, "BITE!", "red")
    hud.hit_player(snap, LUNGE_BITE)
    fx, fy = mimic.facing()
    player.move(snap, snap + 8, (player.x + fx * 1.6, player.y + fy * 1.6), lean=-80)
    player.sword += [(snap + 3, 20.0), (snap + 20, 0.0)]
    player.key(snap + 26, lean=-80)
    player.key(snap + 40)
    hud.caption(snap + 4, "Knocked down. The opening is lost")
    return end, hud.cooldown(snap, COOLDOWN)


def stagger(mimic, player, hud, frame, ready, flank_to):
    """The player circles to its flank; it turns to follow; a heavy hit as it closes staggers it."""
    hud.caption(frame, "Back on your feet. It turns to follow")
    player.move(frame + 2, frame + 30, flank_to, face=mimic.pos)
    frame = mimic.hop(mimic.turn_hop(frame, flank_to), 1)
    strike = frame + 10
    player.move(frame, strike - 2, mimic.ahead(1.25), face=mimic.pos)
    hud.caption(strike - 12, "A heavy hit staggers it")
    player.swing(strike, heavy=True)
    hud.hit_mimic(strike, HEAVY_HIT)
    hud.flash(strike, "STAGGERED", "gold")
    mimic.hold(frame, strike)
    frame = mimic.play(STAGGER, strike)
    player.move(frame, frame + 14, mimic.ahead(3.0), face=mimic.pos)
    return mimic.hold(frame, ready) if frame < ready else frame


def finish(mimic, player, hud, frame):
    """It turns on the player standing beside it and takes two more hits, which kill it."""
    hud.caption(frame, "It can be hit any time. The recovery is just the safe moment")
    frame = mimic.turn_hop(frame, player.pos)
    for strike in (frame - 10, frame + 2):
        player.swing(strike)
        hud.hit_mimic(strike, HIT)
    death = frame + 4
    mimic.hold(frame, death)
    hud.death = death
    hud.caption(death, "Dead. The chest's loot spills out")
    mimic.play(DEATH, death)
    return death + 90
