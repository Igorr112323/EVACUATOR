#!/usr/bin/env python3
"""Create the editable demonstration catalogue, not a substitute for official artwork."""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WHITE, BLACK = '#FFFFFF', '#171717'

def points(values):
    return [{'x': round(x, 4), 'y': round(y, 4)} for x, y in values]

def polygon(values, fill=WHITE):
    return {'type': 'Polygon', 'points': points(values), 'fill': fill}

def line(values, color=WHITE, width=.04):
    return {'type': 'Polyline', 'points': points(values), 'stroke': color, 'strokeWidth': width}

def rect(x, y, w, h, fill=WHITE):
    return {'type': 'Rect', 'x': x, 'y': y, 'width': w, 'height': h, 'fill': fill}

def circle(x, y, r, fill=WHITE):
    return {'type': 'Circle', 'x': x, 'y': y, 'radius': r, 'fill': fill}

def text(x, y, value, size, color=WHITE):
    return {'type': 'Text', 'x': x, 'y': y, 'text': value, 'fontSize': size, 'fill': color}

def mirror(items):
    import copy
    items = copy.deepcopy(items)
    for p in items:
        if 'points' in p:
            for pt in p['points']:
                pt['x'] = round(1 - pt['x'], 4)
        if 'x' in p:
            p['x'] = round(1 - p['x'] - (p.get('width', 0) if p['type'] == 'Rect' else 0), 4)
    return items

def arrow(angle=0, color=WHITE, center=(.5,.5), length=.65, thickness=.14):
    # Arrow polygon, then rotate around its centre.
    cx, cy = center
    coords = [(-.5,-.5),(.10,-.5),(.10,-1),( .5,0),(.10,1),(.10,.5),(-.5,.5)]
    rad = angle * math.pi / 180
    return polygon([(cx + x*length*math.cos(rad)-y*thickness*math.sin(rad),
                     cy + x*length*math.sin(rad)+y*thickness*math.cos(rad)) for x,y in coords], color)

def runner():
    return [circle(.46,.73,.065),
            polygon([(.41,.64),(.52,.60),(.57,.49),(.48,.44),(.39,.53)]),
            line([(.43,.56),(.32,.48),(.23,.51)], width=.055),
            line([(.52,.58),(.61,.52),(.68,.56)], width=.055),
            line([(.47,.45),(.37,.31),(.28,.25)], width=.065),
            line([(.50,.45),(.59,.34),(.70,.30)], width=.065)]

def doorway(left=False):
    items = [line([(.17,.15),(.17,.84),(.78,.84),(.78,.15)], width=.045),
             line([(.25,.18),(.25,.78),(.37,.78)], width=.035)] + runner()
    return mirror(items) if left else items

def directional(direction):
    # A compact running figure with an unambiguous separate arrow.
    man = [circle(.33,.72,.055), line([(.33,.64),(.39,.50),(.32,.38)], width=.075),
           line([(.37,.58),(.24,.49)], width=.04), line([(.32,.38),(.21,.23)], width=.055),
           line([(.32,.38),(.43,.25)], width=.055)]
    if direction == 'left': return mirror(man + [arrow(0, center=(.70,.53), length=.38, thickness=.08)])
    if direction == 'up': return man + [arrow(90, center=(.68,.53), length=.46, thickness=.08)]
    return man + [arrow(0, center=(.70,.53), length=.38, thickness=.08)]

def stairs(direction, left=False):
    base = [line([(.12,.22),(.28,.22),(.28,.34),(.44,.34),(.44,.46),(.60,.46),(.60,.58),(.76,.58)], width=.045),
            arrow(45 if direction == 'up' else -45, center=(.48,.64), length=.38, thickness=.065)]
    return mirror(base) if left else base

def door_action(push=True):
    return [line([(.18,.18),(.18,.82),(.58,.82),(.58,.18)], width=.055),
            circle(.49,.49,.025), arrow(0 if push else 180, center=(.66,.48), length=.35, thickness=.07)]

def assembly():
    return [circle(.5,.57,.075), line([(.5,.48),(.5,.30)], width=.065),
            line([(.5,.40),(.40,.32)], width=.045), line([(.5,.40),(.60,.32)], width=.045),
            arrow(0, center=(.21,.65), length=.24, thickness=.04),
            arrow(180, center=(.79,.65), length=.24, thickness=.04),
            arrow(90, center=(.5,.18), length=.20, thickness=.035)]

def fire_hose():
    return [line([(.22,.77),(.78,.77),(.78,.25),(.22,.25),(.22,.77)], width=.05),
            circle(.50,.51,.20), circle(.50,.51,.12, '#CC1E1E'),
            line([(.50,.51),(.65,.38),(.75,.35)], width=.045)]

def ladder():
    return [line([(.30,.18),(.30,.82)], width=.06), line([(.70,.18),(.70,.82)], width=.06)] + [line([(.30,y),(.70,y)], width=.04) for y in (.28,.42,.56,.70)]

def extinguisher():
    return [rect(.31,.21,.37,.42), circle(.495,.63,.17), rect(.40,.65,.18,.08),
            line([(.49,.73),(.49,.80),(.68,.80)], width=.045),
            line([(.44,.76),(.31,.73),(.22,.58)], width=.045), rect(.40,.32,.19,.035, '#CC1E1E')]

def phone():
    return [polygon([(.20,.72),(.31,.80),(.42,.63),(.35,.56),(.43,.44),(.53,.36),(.60,.44),(.78,.32),(.70,.20),(.60,.20),(.44,.28),(.32,.40),(.22,.57)])]

def hydrant():
    return [rect(.43,.22,.14,.45), rect(.26,.48,.48,.12), rect(.30,.63,.40,.08),
            line([(.50,.68),(.50,.80)], width=.045), line([(.36,.79),(.64,.79)], width=.045),
            rect(.32,.19,.36,.05)]

def fire_button():
    return [line([(.19,.18),(.81,.18),(.81,.82),(.19,.82),(.19,.18)], width=.055),
            circle(.50,.51,.19), circle(.50,.51,.10,'#CC1E1E'), line([(.32,.26),(.68,.26)], width=.04)]

def first_aid():
    return [rect(.40,.19,.20,.62), rect(.19,.40,.62,.20)]

def no_smoking():
    return [rect(.22,.37,.46,.08,BLACK), rect(.70,.37,.07,.08,BLACK),
            line([(.61,.50),(.56,.56),(.61,.63),(.58,.70)], BLACK, .025)]

def electricity():
    return [polygon([(.55,.80),(.31,.45),(.46,.45),(.36,.19),(.70,.57),(.53,.57)],BLACK)]

specs = [
 ('E01-01','Выход здесь (левосторонний)','E','Square',doorway(True),True),
 ('E01-02','Выход здесь (правосторонний)','E','Square',doorway(False),True),
 ('E02-01','Направляющая стрелка вправо','E','Square',[arrow()],False),
 ('E02-02','Направляющая стрелка под углом 45°','E','Square',[arrow(45)],False),
 ('E03','Направление к эвакуационному выходу направо','E','Square',directional('right'),False),
 ('E04','Направление к эвакуационному выходу налево','E','Square',directional('left'),False),
 ('E05','Направление вверх по лестнице направо','E','Square',stairs('up'),False),
 ('E06','Направление вниз по лестнице направо','E','Square',stairs('down'),False),
 ('E07','Направление вверх по лестнице налево','E','Square',stairs('up',True),False),
 ('E08','Направление вниз по лестнице налево','E','Square',stairs('down',True),False),
 ('E09','Дверь эвакуационного выхода','E','Square',[line([(.24,.15),(.24,.85),(.75,.85),(.75,.15)],width=.06),circle(.65,.50,.035),arrow(90,center=(.47,.49),length=.37,thickness=.07)],True),
 ('E10','Запасной выход','E','Square',doorway(False)+[arrow(0,center=(.73,.22),length=.28,thickness=.055)],True),
 ('E11','Направление к эвакуационному выходу прямо','E','Square',directional('up'),False),
 ('E13','Направление по лестнице вниз','E','Square',stairs('down')+[arrow(270,center=(.78,.35),length=.25,thickness=.05)],False),
 ('E15','Направление по лестнице вверх','E','Square',stairs('up')+[arrow(90,center=(.78,.35),length=.25,thickness=.05)],False),
 ('E18','Открывать движением от себя','E','Square',door_action(True),False),
 ('E19','Открывать движением на себя','E','Square',door_action(False),False),
 ('E21','Пункт сбора','E','Square',assembly(),False),
 ('E22','Указатель выхода','E','Rectangle2x1',[text(.14,.40,'ВЫХОД',.22)],True),
 ('E23','Указатель запасного выхода','E','Rectangle2x1',[text(.08,.53,'ЗАПАСНЫЙ',.14),text(.25,.32,'ВЫХОД',.15)],True),
 ('F01-01','Направляющая стрелка пожарной безопасности','F','Square',[arrow()],False),
 ('F02','Пожарный кран','F','Square',fire_hose(),False),
 ('F03','Пожарная лестница','F','Square',ladder(),False),
 ('F04','Огнетушитель','F','Square',extinguisher(),False),
 ('F05','Телефон для использования при пожаре','F','Square',phone(),False),
 ('F07','Пожарный водоисточник / сухотрубный стояк','F','Square',hydrant(),False),
 ('F10','Кнопка пожарной автоматики','F','Square',fire_button(),False),
 ('EC01','Аптечка первой помощи','EC','Square',first_aid(),False),
 ('P01','Запрещается курить','P','CircleSlash',no_smoking(),False),
 ('W09','Опасность поражения электрическим током','W','EquilateralTriangle',electricity(),False),
]
colors = {'E':'#008855','EC':'#008855','F':'#CC1E1E','P':'#FFFFFF','W':'#FFDF00'}
marker = '[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]'
signs = []
for code, name, category, shape, primitives, is_exit in specs:
    signs.append({'code':code, 'name':name, 'officialDescription':name, 'category':category,
                  'shape':shape, 'aspectRatio':2 if shape=='Rectangle2x1' else 1,
                  'minHeightMm':8, 'maxHeightMm':15, 'defaultHeightMm':10,
                  'gostClauseRef':marker+' Код, назначение, изображение и размер не сверены с официальным текстом.',
                  'backgroundColor':colors[category], 'isExit':is_exit, 'primitives':primitives})

base = {'version':'0.1-demo',
        'notice': marker+' Учебные схематичные пиктограммы и параметры; не являются официальной нормативной базой.',
        'parameters': {'defaultHeightMm':10, 'uniformToleranceMm':.01, 'snapToleranceMm':250,
                       'maxAutoSnapDistanceMm':3500, 'pathLineWeightMm':.5, 'pathColor':'#008855',
                       'arrowSpacingMm':3500, 'arrowSizeMm':500, 'overlapThreshold':.15,
                       'offsetStepMm':250, 'maxOffsetSteps':80, 'duplicateToleranceMm':1,
                       'floorFrameWidthMm':600, 'floorFrameHeightMm':400,
                       'localFrameWidthMm':400, 'localFrameHeightMm':300},
        'fireInstructionTemplate': {'approval':'УТВЕРЖДАЮ',
          'title':'ПЛАН ЭВАКУАЦИИ при пожаре',
          'instructions':'При обнаружении пожара сообщите по телефонам 101 и 112, предупредите людей, покиньте здание по указанным путям и следуйте указаниям ответственных лиц. Не пользуйтесь лифтом.'},
        'signs':signs}
amendment = {'signs':[{'code':'E99','name':'Пользовательский учебный знак','officialDescription':'Пользовательский учебный знак',
              'category':'E','shape':'Square','aspectRatio':1,'minHeightMm':8,'maxHeightMm':15,'defaultHeightMm':10,
              'gostClauseRef':marker+' Пользовательский код не является заявлением о соответствии стандарту.',
              'backgroundColor':'#008855','isExit':False,'primitives':[text(.30,.39,'?',.30), {'type':'Arc','x':.5,'y':.5,'radius':.35,'startAngleDegrees':0,'sweepAngleDegrees':270,'stroke':'#FFFFFF','strokeWidth':.035}]}]}
for name, obj in [('gost_12_4_026_2015.json',base),('sample_custom_amendment.json',amendment)]:
    (ROOT/'norms'/name).write_text(json.dumps(obj, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(f'Generated {len(signs)} explicitly unverified demonstration signs')
