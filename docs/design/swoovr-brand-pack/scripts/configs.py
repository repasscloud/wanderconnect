"""Resolve reusable variants and move complete paint settings between layers."""
from copy import deepcopy
LAYERS = ('bird', 'word', 'background')
FIELDS = ('style', 'weights', 'balance', 'angle')

def settings(theme, layer, angle=None):
    return {
        'style': theme.get(layer+'_style', theme.get('style','blend') if layer=='bird' else 'blend'),
        'weights': theme.get(layer+'_weights', theme.get('weights') if layer=='bird' else None),
        'balance': theme.get(layer+'_balance', theme.get('balance','area') if layer=='bird' else 'linear'),
        'angle': angle if angle is not None else theme.get(layer+'_angle',theme.get('angle',0)),
    }

def swap(theme, pair):
    left,right=pair.split('-')
    # Materialise both paints before exchanging them, including inherited settings.
    paints={k:dict(colours=deepcopy(theme[k]),**settings(theme,k)) for k in (left,right)}
    for target,source in ((left,right),(right,left)):
        theme[target]=paints[source]['colours']
        for field in FIELDS:theme[target+'_'+field]=paints[source][field]
    # Rectangle backgrounds use geometric widths, never the bird silhouette's area map.
    if 'background' in (left,right):theme['background_balance']='linear'
    return theme

def resolve(config, themes):
    if not isinstance(config,dict):raise ValueError('Each variant must be a JSON object.')
    allowed=set(LAYERS)|{'base','swap','label','angle','style','balance','weights'}|{k+'_'+f for k in LAYERS for f in FIELDS}
    unknown=set(config)-allowed
    if unknown:raise ValueError('Unknown config fields: '+', '.join(sorted(unknown)))
    base=config.get('base','primary')
    if base not in themes:raise ValueError('Unknown base preset: '+str(base))
    theme=deepcopy(themes[base])
    if config.get('swap'):
        if config['swap'] not in ('bird-background','bird-word','word-background'):raise ValueError('Invalid swap pair.')
        swap(theme,config['swap'])
    for key,value in config.items():
        if key in ('base','swap'):continue
        theme[key]=deepcopy(value)
        if key in LAYERS:
            if key+'_weights' not in config:
                theme[key+'_weights']=config.get('weights') if key=='bird' else None
            if key=='bird' and 'weights' not in config:theme.pop('weights',None)
    return theme
