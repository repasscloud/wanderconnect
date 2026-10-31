#!/usr/bin/env python3
"""Prepare an Azure job start template on stdout; no network or execution. Preserve secretRef inputs."""
import argparse,json,sys
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--container',required=True);p.add_argument('--command',nargs='*');p.add_argument('arguments',nargs=argparse.REMAINDER);args=p.parse_args()
try:
    data=json.load(sys.stdin);containers=data['containers'];chosen=[c for c in containers if c['name']==args.container]
    if len(chosen)!=1:raise ValueError()
    values=args.arguments[1:] if args.arguments[:1]==['--'] else args.arguments
    if not values:raise ValueError()
    chosen[0]['args']=values
    if args.command is not None:chosen[0]['command']=args.command
    json.dump(data,sys.stdout,indent=2);sys.stdout.write('\n')
except (KeyError,TypeError,ValueError):raise SystemExit('Invalid job template/arguments; no execution performed.')
