﻿using System.Windows.Forms;
using AssetStudio;

namespace AssetStudioGUI
{
    public class GameObjectTreeNode : TreeNode
    {
        public GameObject gameObject;

        public GameObjectTreeNode(string name)
        {
            Text = name;
        }

        public GameObjectTreeNode(GameObject gameObject)
        {
            this.gameObject = gameObject;
            Text = gameObject.m_Name;
        }
    }
}
