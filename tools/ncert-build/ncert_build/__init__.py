"""Build-time tooling for Lantern's NCERT layer (D15, D16).

Each stage reads the previous stage's output from the data directory and writes its own, so any
stage can be re-run on its own. Nothing here serves mothers; it only prepares data.
"""
