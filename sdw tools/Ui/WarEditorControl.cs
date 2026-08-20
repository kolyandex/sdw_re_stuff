using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SdwEditor.Formats;
using SdwEditor.Render;

namespace SdwEditor.Ui
{
    internal sealed class WarEditorControl : UserControl
    {
        private readonly TreeView _tree = new TreeView();
        private readonly ModelPreviewControl _preview = new ModelPreviewControl();
        private readonly TextBox _props = new TextBox();
        private WarFile _war;

        public WarEditorControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Panel;
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, BackColor = Theme.Line };
            Theme.LayoutSplit(split, 0.20f);
            Controls.Add(split);
            Theme.StyleTree(_tree);
            _tree.Dock = DockStyle.Fill;
            _tree.AfterSelect += TreeOnAfterSelect;
            split.Panel1.Controls.Add(_tree);

            SplitContainer right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Theme.Line };
            Theme.LayoutSplit(right, 0.78f);
            _preview.Dock = DockStyle.Fill;
            _props.Dock = DockStyle.Fill;
            _props.Multiline = true;
            _props.ReadOnly = true;
            _props.BackColor = Theme.Bg;
            _props.ForeColor = Theme.Text;
            _props.BorderStyle = BorderStyle.None;
            _props.Font = Theme.Mono;
            _props.ScrollBars = ScrollBars.Vertical;
            right.Panel1.Controls.Add(_preview);
            right.Panel2.Controls.Add(_props);
            split.Panel2.Controls.Add(right);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down
                || key == Keys.Space || key == Keys.N || key == Keys.P
                || key == Keys.OemOpenBrackets || key == Keys.OemCloseBrackets)
            {
                return _preview.HandleCameraKey(keyData);
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void Bind(WarFile war)
        {
            object keep = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag;
            BindCore(war, keep);
        }

        public void ApplyLanguage()
        {
            if (_war != null) _war.Relabel();
            object keep = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag;
            BindCore(_war, keep);
            _preview.ApplyLanguage();
        }

        private void BindCore(WarFile war, object keep)
        {
            _war = war;
            _tree.Nodes.Clear();
            _preview.ClearPreview();
            _props.Text = "";
            if (war == null) return;

            TreeNode geom = Node(Loc.T("war_geom"));
            TreeNode actors = Node(Loc.T("war_actors"));
            TreeNode scene = Node(Loc.T("war_scene"));
            TreeNode sky = Node(Loc.T("war_sky"));
            TreeNode coll = Node(Loc.T("war_coll"));
            TreeNode other = Node(Loc.T("war_other"));
            TreeNode all = new TreeNode(Loc.T("war_all"));
            all.Tag = "level";
            _tree.Nodes.Add(all);

            foreach (WarResource r in war.Resources)
            {
                TreeNode n = new TreeNode(r.Label) { Tag = r };
                switch (r.Kind)
                {
                    case WarKind.StaticMesh:
                        (r.IsActor ? actors : geom).Nodes.Add(n); break;
                    case WarKind.AnimatedMesh:
                        actors.Nodes.Add(n); break;
                    case WarKind.Scenaric:
                        scene.Nodes.Add(n); break;
                    case WarKind.Skybox:
                        sky.Nodes.Add(n); break;
                    case WarKind.CollisionMap:
                    case WarKind.CollisionPolys:
                        coll.Nodes.Add(n); break;
                    default:
                        other.Nodes.Add(n); break;
                }
            }
            geom.Expand();
            actors.Expand();
            scene.Expand();
            if (keep != null) SelectByTag(keep);
        }

        private void SelectByTag(object tag)
        {
            TreeNode n = FindTag(_tree.Nodes, tag);
            if (n != null) _tree.SelectedNode = n;
        }

        private static TreeNode FindTag(TreeNodeCollection nodes, object tag)
        {
            string key = tag as string;
            foreach (TreeNode n in nodes)
            {
                if (n.Tag == tag) return n;
                if (key != null && key.Equals(n.Tag as string)) return n;
                TreeNode inner = FindTag(n.Nodes, tag);
                if (inner != null) return inner;
            }
            return null;
        }

        private TreeNode Node(string title)
        {
            TreeNode n = new TreeNode(title);
            _tree.Nodes.Add(n);
            return n;
        }

        private void TreeOnAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_war == null || e.Node == null) return;
            if ("level".Equals(e.Node.Tag as string))
            {
                List<WarTri> all = new List<WarTri>();
                List<WarTri> sky = new List<WarTri>();
                List<WarLevelActor> actors = new List<WarLevelActor>();
                int placed = 0;
                foreach (WarResource r in _war.Resources)
                {
                    if (r.Kind == WarKind.StaticMesh && !r.IsActor)
                    {
                        all.AddRange(r.Tris);
                    }
                    else if (r.Kind == WarKind.Skybox)
                    {
                        sky.AddRange(r.Tris);
                    }
                    else if (WarFile.IsWorldObject(r))
                    {
                        WarResource model;
                        if (_war.IsAnimatedWorld(r, out model))
                        {
                            WarLevelActor actor = new WarLevelActor();
                            actor.Model = model;
                            actor.Inst = r;
                            actor.Clip = WarPose.DefaultClip(model);
                            actors.Add(actor);
                            placed++;
                        }
                        else
                        {
                            List<WarTri> inst = _war.MeshFor(r);
                            if (inst.Count > 0)
                            {
                                all.AddRange(inst);
                                placed++;
                            }
                        }
                    }
                }
                _preview.SetLevel(all, sky, actors, Loc.F("level_hint", placed, sky.Count));
                _props.Text = Loc.F("level_props", all.Count, placed, actors.Count, sky.Count, _war.Fog.ToString());
                return;
            }
            WarResource res = e.Node.Tag as WarResource;
            if (res == null) return;
            List<WarTri> mesh = _war.MeshFor(res);
            WarResource anim = res;
            if (res.Kind == WarKind.Scenaric && res.ModelRef >= 0 && res.ModelRef < _war.Resources.Count)
            {
                anim = _war.Resources[res.ModelRef];
            }
            if (anim.Kind == WarKind.AnimatedMesh && anim.Anims != null && anim.Anims.Count > 0)
            {
                _preview.PlayAnimated(anim, res.Label);
            }
            else
            {
                _preview.SetMesh(mesh, res.Label);
            }
            System.Text.StringBuilder anims = new System.Text.StringBuilder();
            if (res.Anims != null)
            {
                for (int i = 0; i < res.Anims.Count; i++)
                {
                    if (i > 0) anims.Append(", ");
                    anims.Append(res.Anims[i].Name);
                }
            }
            _props.Text = Loc.F(
                "res_props",
                res.Index, res.Kind, res.Flags, res.Pointer, res.VertexCount, res.ChunkCount, res.BoneCount, mesh.Count,
                res.Anims == null ? 0 : res.Anims.Count, anims.Length == 0 ? "-" : anims.ToString(),
                res.ClassId, res.ClassId >= 0 ? Catalog.ClassName(res.ClassId) : "-",
                res.ModelRef, res.Position.X, res.Position.Y, res.Position.Z,
                res.RotPitch, res.RotYaw, res.RotRoll, _war.Version);
        }
    }
}
