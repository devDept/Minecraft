using System.Configuration;
using System.Diagnostics;
using devDept.Eyeshot.Control;
using devDept.Eyeshot.Entities;
using devDept.Eyeshot;
using devDept.Geometry;
using devDept.Graphics;

namespace Minecraft
{
    public partial class Form1 : Form
    {
        private int _activeMatIndex = 2;

        // dimensions
        private const int MatrixDepth = 16;
        private const int MatrixWidth = 16;
        private const int MatrixHeight = 16;
        private const int BlockSize = 10;

        // material cubical matrix 
        private readonly int[,,] _matrix3D = new int[MatrixDepth, MatrixWidth, MatrixHeight];

        // material names
        private const string DirtFace = "dirt";
        private const string GrassFace = "grass";
        private const string GrassSideFace = "grass_side";
        private const string WoodTopFace = "wood_top";
        private const string WoodFace = "wood";
        private const string LeavesFace = "leaves";
        private const string WaterFace = "water";

        // available blocks
        private readonly Block _airBlock = new Block(DirtFace, DirtFace, DirtFace, DirtFace, DirtFace, DirtFace);
        private readonly Block _grassBlock = new Block(DirtFace, GrassSideFace, GrassSideFace, GrassSideFace, GrassSideFace, GrassFace);
        private readonly Block _dirtBlock = new Block(DirtFace);
        private readonly Block _woodBlock = new Block(WoodTopFace, WoodFace, WoodFace, WoodFace, WoodFace, WoodTopFace);
        private readonly Block _leavesBlock = new Block(LeavesFace);

        public Form1()
        {
            InitializeComponent();
            
            // disable shadows for this display mode
            design1.Rendered.ShadowMode = shadowType.None;

            // selection colors
			design1.Selection.Color = Color.Transparent;
            design1.Selection.ColorDynamic = Color.FromArgb(25, Color.Black);
            design1.Selection.HaloOuterColorDynamic = Color.Transparent;
            design1.Selection.HaloInnerColorDynamic = Color.Black;

            // never upside down
            design1.ActiveViewport.Rotate.RotationMode = rotationType.Turntable;

            // disable minimum frame rate control
            design1.MinimumFramerate = 0;
        }

        protected override void OnLoad(EventArgs e)
        {
            // activate selection
            design1.ActionMode = actionType.SelectVisibleByPickDynamic;

            // opaque materials
            design1.Materials.Add(GrassFace, Resources.default_grass.ToByteArray());
            design1.Materials.Add(GrassSideFace, Resources.default_grass_side.ToByteArray());
            design1.Materials.Add(DirtFace, Resources.default_dirt.ToByteArray());
            design1.Materials.Add(WoodFace, Resources.default_tree.ToByteArray());
            design1.Materials.Add(WoodTopFace, Resources.default_tree_top.ToByteArray());
            // transparent materials
            design1.Materials.Add(new Material(LeavesFace, Resources.default_leaves.ToByteArray())
            {
                Diffuse = Color.FromArgb(254, Color.White)
            });
			
            design1.Materials.Add(new Material(WaterFace, Resources.default_water_transp.ToByteArray())
            {
                Diffuse = Color.FromArgb(254, Color.White)
            });
            
            // set properties common to all materials
            foreach (Material mat in design1.Materials)
            {
                mat.MagnifyingFunction = textureFilteringFunctionType.Nearest;
                mat.LoadTexture(design1.RenderContext);
                mat.Environment = 0;
            }

            // initial ground setup
            Entity[] ground = new Entity[MatrixWidth * MatrixDepth];

            int k = 0;

            for (int j = 0; j < MatrixWidth; j++)
            {
                for (int i = 0; i < MatrixDepth; i++)
                {
                    string matName = GetMaterialByIndex(2).Top;
                    bool selectable = true;

                    // water area
                    if ((i < 8 && j == 1) ||
                        (i < 9 && j == 0) ||
                        (i < 5 && j == 2) ||
                        (i < 3 && j == 3) ||
                        (i < 2 && j == 4) ||
                        (j < 6 && i == 1) ||
                        (j < 8 && i == 0))
                    {
                        matName = WaterFace;
                        selectable = false;
                    }

                    Mesh face = CreateFace(
                        new Point3D(i * BlockSize, j * BlockSize, 0),
                        new Point3D(i * BlockSize + BlockSize, j * BlockSize, 0),
                        new Point3D(i * BlockSize + BlockSize, j * BlockSize + BlockSize, 0),
                        new Point3D(i * BlockSize, j * BlockSize + BlockSize, 0), matName);

                    face.Selectable = selectable;

                    ground[k++] = face;
                }
            }

            design1.Entities.AddRange(ground);

            base.OnLoad(e);
        }
        
        /// <summary>
        /// Adds a cube adjacent to the selected face.
        /// </summary>
        private void design1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Mesh first = (Mesh) e.AddedItems[0].Item;

                Point3D min = first.BoxMin;

                Vector3D dir = first.Normals[0];

                int[] position = new int[3];

                for (int i = 0; i < 3; i++)
                {
                    position[i] = (int) min[i] / BlockSize;
                    
                    if (dir.X == -1 || dir.Y == -1 || dir.Z == -1)
                    {
                        position[i] += (int)dir[i];
                    }
                }

                // A clicked boundary face can map to a cell outside the 16x16x16 world:
                // there is nothing to place there, and the neighbor lookups below would
                // index _matrix3D out of range, so bail out.
                if (position[0] < 0 || position[0] >= MatrixDepth ||
                    position[1] < 0 || position[1] >= MatrixWidth ||
                    position[2] < 0 || position[2] >= MatrixHeight)
                {
                    return;
                }

                _matrix3D[position[0], position[1], position[2]] = _activeMatIndex;

                bool[] addFace = new bool[6];

                // bottom
                if (position[2] > 0 && _matrix3D[position[0], position[1], position[2] - 1] == 0)
                {
                    addFace[0] = true;
                }

                // front
                if (position[1] == 0 || position[1] > 0 && _matrix3D[position[0], position[1] - 1, position[2]] == 0)
                {
                    addFace[1] = true;
                }  

                // right
                if (position[0] == MatrixWidth - 1 || position[0] < MatrixWidth-1 && _matrix3D[position[0]+1, position[1], position[2]] == 0)
                {
                    addFace[2] = true;
                }

                // rear
                if (position[1] == MatrixDepth - 1 || position[1] < MatrixDepth - 1 && _matrix3D[position[0], position[1] + 1, position[2]] == 0)
                {
                    addFace[3] = true;
                }

                // left
                if (position[0] == 0 || position[0] > 0 &&_matrix3D[position[0] - 1, position[1], position[2]] == 0)
                {
                    addFace[4] = true;
                }

                // top
                if (position[2] == MatrixHeight - 1 || position[2] < MatrixHeight-1 && _matrix3D[position[0], position[1], position[2] + 1] == 0)
                {
                    addFace[5] = true;
                }

                if (dir.X == -1 || dir.Y == -1 || dir.Z == -1)
                {
                    min += dir * BlockSize;
                }

                design1.Entities.AddRange(DrawCube(min.X, min.Y, min.Z, addFace));

                // Force the regen here: during dynamic picking a MouseMove can draw these
                // meshes before the deferred compile runs, throwing "entity not properly compiled".
                design1.Entities.Regen();
                
                design1.Invalidate();
            }
        }

        /// <summary>
        /// Creates the visible faces of a cube at the given position.
        /// </summary>
        private Entity[] DrawCube(double x, double y, double z, bool[] drawFaceList)
        {
            Block block = GetMaterialByIndex(_activeMatIndex);

            if (block == _airBlock)
            {
                return new Entity[0];
            }

            Point3D blockMin = new Point3D(x, y, z);
            Point3D blockMax = new Point3D(x + BlockSize, y + BlockSize, z + BlockSize);

            List<Entity> faces = new List<Entity>(6);
            
            if (drawFaceList[0])
            {
                Mesh bottom = CreateFace(
                    new Point3D(x, y, z),
                    new Point3D(x + BlockSize, y, z),
                    new Point3D(x + BlockSize, y + BlockSize, z),
                    new Point3D(x, y + BlockSize, z), block.Bottom);
                bottom.FlipNormal();
                faces.Add(bottom);
            }

            if (drawFaceList[1])
            {
                Mesh front = CreateFace(
                    new Point3D(x, y, z),
                    new Point3D(x + BlockSize, y, z),
                    new Point3D(x + BlockSize, y, z + BlockSize),
                    new Point3D(x, y, z + BlockSize), block.Front);
                faces.Add(front);
            }

            if (drawFaceList[2])
            {
                Mesh right = CreateFace(
                
                    new Point3D(x + BlockSize, y, z),
                    new Point3D(x + BlockSize, y + BlockSize, z),
                    new Point3D(x + BlockSize, y + BlockSize, z + BlockSize),
                    new Point3D(x + BlockSize, y, z + BlockSize), block.Right);
                faces.Add(right);
            }

            if (drawFaceList[3])
            {
                Mesh rear = CreateFace(
                
                    new Point3D(x, y + BlockSize, z),
                    new Point3D(x + BlockSize, y + BlockSize, z),
                    new Point3D(x + BlockSize, y + BlockSize, z + BlockSize),
                    new Point3D(x, y + BlockSize, z + BlockSize), block.Rear);
                rear.FlipNormal();
                faces.Add(rear);
            }

            if (drawFaceList[4])
            {
                Mesh left = CreateFace(
                
                    new Point3D(x, y, z),
                    new Point3D(x, y + BlockSize, z),
                    new Point3D(x, y + BlockSize, z + BlockSize),
                    new Point3D(x, y, z + BlockSize), block.Left);
                left.FlipNormal();
                faces.Add(left);
			}

            if (drawFaceList[5])
            {
                Mesh top = CreateFace(
                    new Point3D(x, y, z + BlockSize),
                    new Point3D(x + BlockSize, y, z + BlockSize),
                    new Point3D(x + BlockSize, y + BlockSize, z + BlockSize),
                    new Point3D(x, y + BlockSize, z + BlockSize), block.Top);
                faces.Add(top);
            }
  
            RemoveAdjacentFaces(blockMin, blockMax);

            return faces.ToArray();
        }

        /// <summary>
        /// Creates a square textured mesh from four corner points.
        /// </summary>
        private Mesh CreateFace(Point3D v1, Point3D v2, Point3D v3, Point3D v4, string matName)
        {
            Mesh squareFace = new Mesh(new Point3D[] {v1, v2, v3, v4}, new IndexTriangle[] { new IndexTriangle(0, 1, 2), new IndexTriangle(0, 2, 3) });

            squareFace.ApplyMaterial(matName, textureMappingType.Cubic, 1, 1);

            // no edges or silhouettes
            squareFace.LightWeight = true;

            return squareFace;
        }

        /// <summary>
        /// Removes existing adjacent faces.
        /// </summary>
        private void RemoveAdjacentFaces(Point3D blockMin, Point3D blockMax)
        {
            const double inflateBy = BlockSize / 10.0;

            for (int j = design1.Entities.Count - 1; j >= 0; j--)
	        {
		        Entity ent = design1.Entities[j];
                
                if (Utility.IsPointInside(ent.BoxMin, blockMin, blockMax, inflateBy) &&
                    Utility.IsPointInside(ent.BoxMax, blockMin, blockMax, inflateBy))
		        {
			        design1.Entities.RemoveAt(j);
		        }
	        }
        }
        
        /// <summary>
        /// Gets block by index.
        /// </summary>
        private Block GetMaterialByIndex(int i)
        {
            switch (i)
            {
                case 1:
                    return _dirtBlock;

                case 2:
                    return _grassBlock;

                case 3:
                    return _woodBlock;

                case 4:
                    return _leavesBlock;

                default:
                    return _airBlock;
            }
        }

        /// <summary>
        /// Updates the active material from the selected radio button.
        /// </summary>
        private void radioButtonMaterial_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonDirt.Checked)
            {
                _activeMatIndex = 1;
            } 
            else if (radioButtonGrass.Checked)
            {
                _activeMatIndex = 2;
            }
            else if (radioButtonWood.Checked)
            {
                _activeMatIndex = 3;
            }
            else if (radioButtonLeaves.Checked)
            {
                _activeMatIndex = 4;
            }
        }
    }

    /// <summary>
    /// Defines block faces material.
    /// </summary>
    public class Block
    {
        public readonly string Top;
        public readonly string Bottom;
        public readonly string Left;
        public readonly string Right;
        public readonly string Front;
        public readonly string Rear;

        /// <summary>
        /// Initializes a block with a specific material for each face.
        /// </summary>
        public Block(string bottom, string front, string right, string rear, string left, string top)
        {
            Top = top;
            Bottom = bottom;
            Left = left;
            Right = right;
            Front = front;
            Rear = rear;
        }

        /// <summary>
        /// Initializes a block with the same material on all faces.
        /// </summary>
        public Block(string same)
        {
            Top = same;
            Bottom = same;
            Left = same;
            Right = same;
            Front = same;
            Rear = same;
        }
    }
}