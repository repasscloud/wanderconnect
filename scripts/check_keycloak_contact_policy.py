#!/usr/bin/env python3
"""Compile/check a pinned provider in private space; optionally export a new public-code JAR. No realm mutation."""
import argparse
import os
import re
from pathlib import Path
import shutil
import subprocess
import tempfile

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument("--java-home",type=Path,default=os.environ.get("JAVA_HOME"))
parser.add_argument("--keycloak-libs",type=Path,required=True,help="Public runtime libraries from pinned Keycloak26.8.1, never realm/database exports")
parser.add_argument("--output-jar",type=Path,help="New output path; refuses to overwrite an existing artifact")
args=parser.parse_args()
root=Path(__file__).resolve().parent.parent
jdk_home=args.java_home
compiler=str(jdk_home/"bin/javac") if jdk_home else shutil.which("javac")
runtime=str(jdk_home/"bin/java") if jdk_home else shutil.which("java")
jar=str(jdk_home/"bin/jar") if jdk_home else shutil.which("jar")
if not compiler or not runtime or not jar:parser.error("A Java21+ JDK is required; set --java-home explicitly.")
if not (args.keycloak_libs/"org.keycloak.keycloak-server-spi-26.8.1.jar").is_file():parser.error("Expected public libraries from Keycloak26.8.1.")
if args.output_jar and (args.output_jar.exists() or not args.output_jar.parent.is_dir()):parser.error("JAR target must be new and its parent must exist.")
classpath=str(args.keycloak_libs/"*")
base=root/"containers/keycloak/contact-policy/src"
with tempfile.TemporaryDirectory(prefix="swoovr-contact-policy-check-") as private:
    main=Path(private)/"main";tests=Path(private)/"tests"
    main.mkdir(mode=0o700);tests.mkdir(mode=0o700)
    subprocess.run([compiler,"--release","21","-classpath",classpath,"-d",str(main),*sorted(str(p) for p in (base/"main/java").rglob("*.java"))],check=True,cwd=root)
    resources=base/"main/resources"
    if resources.exists():shutil.copytree(resources,main,dirs_exist_ok=True)
    contract=(root/"src/Swoovr.Contracts/ApiContract.cs").read_text()
    values={name:re.search(r'public const string '+name+r' = "([^"\n]+)";',contract) for name in ["Header","Initial"]}
    if not all(values.values()):raise RuntimeError("Canonical API contract could not be generated.")
    (main/"swoovr-api-contract.properties").write_text("header="+values["Header"].group(1)+"\ninitial="+values["Initial"].group(1)+"\n")

    subprocess.run([compiler,"--release","21","-classpath",os.pathsep.join([str(main),classpath]),"-d",str(tests),*sorted(str(p) for p in (base/"test/java").rglob("*.java"))],check=True,cwd=root)
    subprocess.run([runtime,"-classpath",os.pathsep.join([str(tests),str(main),classpath]),"app.swoovr.identity.RetirementPolicyChecks"],check=True,cwd=root)
    subprocess.run([runtime,"-classpath",os.pathsep.join([str(tests),str(main),classpath]),"app.swoovr.identity.MailerSendChecks"],check=True,cwd=root)
    if args.output_jar:
        subprocess.run([jar,"--create","--file",str(args.output_jar.resolve()),"-C",str(main),"."],check=True,cwd=root)
        print("Public-code provider artifact:",args.output_jar)
print("Unit policy only: no provider installed, realm changed, mailbox ownership verified or delivery established.")
