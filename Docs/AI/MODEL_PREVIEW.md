# Unity model preview integration

This branch collects existing draft assets for inspection in one Unity project. It is not an approval or merge of their source pull requests.

- Conductor and wagon corner: PR #21, `feature/9-art-followup-review`, commit `9d1c4c6611e2258a940a3ce9ded05fec07bd40b1`.
- Locomotive, regional wagon, tender and cattle wagon: PR #23, `feature/22-locomotive`, commit `9058b04ebcb018df38b850655652eb24ab8a38d9`.
- Unity package baseline: PR #26, `fix/2-unity-import`.

The original authoring scripts and their shared tooling remain in the source branches. Do not treat this asset-only preview as a replacement for those branches; coordinate future changes with their owners and resolve the final source integration during review. No scene, gameplay, networking or final stylized shader is supplied here.

The assets were imported in Unity 6000.5.7f1 with their accompanying metadata and material references. The intended painted rendering, rigs, animation and multiplayer behaviour still require separate validation. Existing Unity-generated material/settings changes in the working tree predate this preview and are not part of its commit.
