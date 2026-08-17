#!/usr/bin/env python3
"""
ISE Elite V7.10.3 Forensic Analyzer R1

Research-only analyzer for V7.10.2 ATAS raw recorder output.
Uses recorder capture UTC timestamp as canonical time and converts to America/Chicago.
Produces minute-level BBO, aggressive-trade, MBO add/change/delete and exploratory pressure metrics.

The pressure proxy is exploratory and MUST NOT be treated as a live signal until it repeats across untouched sessions.
"""

from __future__ import annotations
import argparse, re, shutil, tempfile, zipfile
from pathlib import Path
import numpy as np
import pandas as pd

TZ = "America/Chicago"
MBO_COLS = ["capture_ts","event_ts","event_type","side","order_id","priority","price","qty","instance","instrument"]
TRADE_COLS = ["capture_ts","event_ts","data_type","direction","price","qty","passive_order_id","aggressor_order_id","instance","instrument"]
BBO_COLS = ["capture_ts","bid","ask","spread","instance","instrument"]


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("input", help="V7.10.2 capture ZIP or folder")
    p.add_argument("--out", default="V7.10.3_Output")
    p.add_argument("--chunk", type=int, default=400000)
    return p.parse_args()


def materialize_input(input_path: Path):
    if input_path.is_dir(): return input_path, None
    if input_path.suffix.lower() == ".zip":
        tmp = Path(tempfile.mkdtemp(prefix="ise_v7103_"))
        with zipfile.ZipFile(input_path, "r") as z: z.extractall(tmp)
        return tmp, tmp
    raise ValueError("Input must be a V7.10.2 folder or ZIP")


def sessions(root: Path):
    rows=[]
    for p in root.rglob("V7.10.2-session-*.txt"):
        txt=p.read_text(errors="ignore")
        iid=re.search(r"Instance=(\S+)",txt); ins=re.search(r"Instrument=(\S+)",txt); st=re.search(r"StartedUtc=(\S+)",txt)
        if not iid: continue
        r={"instance":iid.group(1),"instrument":ins.group(1) if ins else "","started_utc":st.group(1) if st else "","session_file":str(p)}
        for stream in ("mbo","trades","bbo","depth","health"):
            cand=list(root.rglob(f"V7.10.2-{stream}-*-{iid.group(1)}.tsv"))
            if cand:
                f=max(cand,key=lambda x:x.stat().st_size); r[f"{stream}_file"]=str(f); r[f"{stream}_bytes"]=f.stat().st_size
        rows.append(r)
    return pd.DataFrame(rows)


def choose_primary(inv):
    out=[]
    for family,pat in [("MNQ",r"^MNQ"),("MGC",r"^MGC")]:
        x=inv[inv.instrument.str.match(pat,na=False)].copy()
        if not x.empty and "mbo_bytes" in x:
            r=x.sort_values("mbo_bytes",ascending=False).iloc[0].to_dict(); r["family"]=family; out.append(r)
    return out


def aggregate_bbo(path, chunk):
    parts=[]
    for c in pd.read_csv(path,sep="\t",names=BBO_COLS,chunksize=chunk):
        c["capture_ts"]=pd.to_datetime(c.capture_ts,utc=True,errors="coerce"); c=c.dropna(subset=["capture_ts"])
        c["minute_utc"]=c.capture_ts.dt.floor("min"); c["mid"]=(pd.to_numeric(c.bid,errors="coerce")+pd.to_numeric(c.ask,errors="coerce"))/2; c["spread"]=pd.to_numeric(c.spread,errors="coerce")
        parts.append(c.groupby("minute_utc").agg(mid_open=("mid","first"),mid_high=("mid","max"),mid_low=("mid","min"),mid_close=("mid","last"),spread_median=("spread","median"),bbo_samples=("mid","size")))
    x=pd.concat(parts).sort_index()
    y=x.groupby(level=0).agg(mid_open=("mid_open","first"),mid_high=("mid_high","max"),mid_low=("mid_low","min"),mid_close=("mid_close","last"),spread_median=("spread_median","median"),bbo_samples=("bbo_samples","sum"))
    y["minute_range"]=y.mid_high-y.mid_low; y["minute_return"]=y.mid_close-y.mid_open
    return y


def aggregate_trades(path, chunk):
    parts=[]
    for c in pd.read_csv(path,sep="\t",names=TRADE_COLS,chunksize=chunk):
        c["capture_ts"]=pd.to_datetime(c.capture_ts,utc=True,errors="coerce"); c["qty"]=pd.to_numeric(c.qty,errors="coerce").fillna(0); c=c.dropna(subset=["capture_ts"]); c["minute_utc"]=c.capture_ts.dt.floor("min")
        parts.append(c.groupby(["minute_utc","direction"]).agg(trade_count=("qty","size"),trade_qty=("qty","sum")).reset_index())
    z=pd.concat(parts).groupby(["minute_utc","direction"])[["trade_count","trade_qty"]].sum().reset_index(); out=pd.DataFrame(index=sorted(z.minute_utc.unique()))
    for d in sorted(z.direction.dropna().unique()):
        s=z[z.direction==d].set_index("minute_utc"); out[f"trade_{str(d).lower()}_count"]=s.trade_count; out[f"trade_{str(d).lower()}_qty"]=s.trade_qty
    return out.fillna(0)


def aggregate_mbo(path, chunk):
    parts=[]
    for c in pd.read_csv(path,sep="\t",names=MBO_COLS,chunksize=chunk):
        c["capture_ts"]=pd.to_datetime(c.capture_ts,utc=True,errors="coerce"); c["qty"]=pd.to_numeric(c.qty,errors="coerce").fillna(0); c=c.dropna(subset=["capture_ts"]); c["minute_utc"]=c.capture_ts.dt.floor("min")
        parts.append(c.groupby(["minute_utc","event_type","side"]).agg(event_count=("qty","size"),reported_qty=("qty","sum")).reset_index())
    z=pd.concat(parts).groupby(["minute_utc","event_type","side"])[["event_count","reported_qty"]].sum().reset_index(); out=pd.DataFrame(index=sorted(z.minute_utc.unique()))
    for et in sorted(z.event_type.dropna().unique()):
        for side in sorted(z.side.dropna().unique()):
            s=z[(z.event_type==et)&(z.side==side)].set_index("minute_utc"); key=f"{str(et).lower()}_{str(side).lower()}"; out[f"{key}_count"]=s.event_count; out[f"{key}_qty"]=s.reported_qty
    return out.fillna(0)


def assemble(mbo,trades,bbo):
    x=bbo.join(trades,how="outer").join(mbo,how="outer").sort_index().fillna(0)
    for c in ("trade_buy_qty","trade_sell_qty","new_bid_qty","new_ask_qty","delete_bid_qty","delete_ask_qty"):
        if c not in x: x[c]=0.0
    x["trade_delta"]=x.trade_buy_qty-x.trade_sell_qty
    den=x.trade_buy_qty+x.trade_sell_qty; x["aggressive_buy_ratio"]=np.where(den>0,x.trade_buy_qty/den,np.nan)
    x["new_imbalance"]=x.new_bid_qty-x.new_ask_qty; x["delete_imbalance"]=x.delete_ask_qty-x.delete_bid_qty
    x["pressure_proxy"]=x.trade_delta+0.10*x.new_imbalance+0.10*x.delete_imbalance
    cnt=[c for c in x if c.endswith("_count") and c.startswith(("new_","change_","delete_","snapshot_"))]; x["mbo_events"]=x[cnt].sum(axis=1) if cnt else 0
    x["minute_ct"]=x.index.tz_convert(TZ)
    return x


def window_summary(x,s,e):
    a=pd.Timestamp(s,tz=TZ); b=pd.Timestamp(e,tz=TZ); w=x[(x.minute_ct>=a)&(x.minute_ct<b)]
    if w.empty:return None
    return {"net_points":float(w.mid_close.iloc[-1]-w.mid_open.iloc[0]),"range":float(w.mid_high.max()-w.mid_low.min()),"trade_delta":float(w.trade_delta.sum()),"new_imbalance":float(w.new_imbalance.sum()),"delete_imbalance":float(w.delete_imbalance.sum()),"pressure_proxy":float(w.pressure_proxy.sum())}


def main():
    a=parse_args(); inp=Path(a.input).expanduser().resolve(); out=Path(a.out).expanduser().resolve(); out.mkdir(parents=True,exist_ok=True); root,tmp=materialize_input(inp)
    try:
        inv=sessions(root); inv.to_csv(out/"session_inventory.csv",index=False); prim=choose_primary(inv)
        report=["# ISE Elite V7.10.3 — Initial Forensic Analysis","","**Status:** Research only. No production signal logic.","","Canonical time: recorder capture timestamp converted to America/Chicago.",""]
        for r in prim:
            fam=r["family"]; b=aggregate_bbo(Path(r["bbo_file"]),a.chunk); t=aggregate_trades(Path(r["trades_file"]),a.chunk); m=aggregate_mbo(Path(r["mbo_file"]),a.chunk); x=assemble(m,t,b); x.to_csv(out/f"{fam}_minute_features.csv",index=False)
            report += [f"## {fam} primary capture","",f"- Instance: `{r['instance']}`",f"- Instrument: `{r['instrument']}`",f"- Minute features: {len(x):,}",f"- First CT minute: {x.minute_ct.min()}",f"- Last CT minute: {x.minute_ct.max()}",""]
            if fam=="MNQ":
                c2=x[(x.minute_ct>=pd.Timestamp("2026-08-16 20:45",tz=TZ))&(x.minute_ct<pd.Timestamp("2026-08-16 21:36",tz=TZ))].copy(); c2.to_csv(out/"condition2_mnq_2045_2135.csv",index=False)
                report += ["### Condition 2 — MNQ late transition",""]
                for name,s,e in [("Pre-transition / rotation","2026-08-16 20:45","2026-08-16 21:08"),("Trend forming","2026-08-16 21:08","2026-08-16 21:21"),("Expansion burst","2026-08-16 21:21","2026-08-16 21:24")]:
                    q=window_summary(x,s,e)
                    if q: report += [f"**{name}**",f"- Net price: {q['net_points']:+.2f} points; range: {q['range']:.2f}",f"- Aggressive trade delta: {q['trade_delta']:+.0f}",f"- New-order imbalance: {q['new_imbalance']:+.0f}",f"- Delete imbalance: {q['delete_imbalance']:+.0f}",f"- Exploratory pressure proxy: {q['pressure_proxy']:+.1f}",""]
                report += ["Interpretation: candidate state transition only; not a proven signal.",""]
        (out/"V7_10_3_INITIAL_REPORT.md").write_text("\n".join(report),encoding="utf-8")
        print(f"V7.10.3 analysis complete: {out}")
    finally:
        if tmp: shutil.rmtree(tmp,ignore_errors=True)

if __name__=="__main__": main()
