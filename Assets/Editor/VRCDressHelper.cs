/*
	VRCDressHelper Ver 0.1
	Created by Qyuming
	https://github.com/qyumingdev/VRCDressHelper
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Qyudev.Editor
{
	class VRCDressHelper : EditorWindow
	{
		class BoneData
		{
			public Transform            transform       = null;
			public HumanBodyBones       type            = HumanBodyBones.LastBone;

			public BoneData( Transform transform, HumanBodyBones type )
			{
				this.transform = transform;
				this.type = type;
			}
		}

		bool                _isIncludeInactive                      = false;

		GameObject          _go_Avatar                              = null;
		GameObject          _go_Dress                               = null;
		GUIStyle            _style_Error                            = null;

		GameObject          _go_OriginalSkinnedMeshRenderer         = null;

		[MenuItem( "Tools/VRCDressHelper", false )]
		static void CreateWindow()
		{
			var window = EditorWindow.GetWindow( typeof( VRCDressHelper ), false, "VRC Dress Helper" );
			window.Show();
		}

		void Awake()
		{
			_style_Error = new GUIStyle();
			_style_Error.normal.textColor = Color.red;
		}

		void OnGUI()
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label( "Avatar", GUILayout.Width( 60f ) );
			_go_Avatar = EditorGUILayout.ObjectField( _go_Avatar, typeof( GameObject ), true ) as GameObject;
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Label( "Dress", GUILayout.Width( 60f ) );
			_go_Dress = EditorGUILayout.ObjectField( _go_Dress, typeof( GameObject ), true ) as GameObject;
			GUILayout.EndHorizontal();

			GUILayout.Space( 10f );
			GUILayout.Label( "---------- Main ----------" );

			_GUILayout_TransferBonesToTarget();
			_GUILayout_SelectDressBones();

			GUILayout.Space( 10f );
			GUILayout.Label( "---------- Alt ----------" );

			if( !_GUILayout_TransferBonesToTarget_BasedOnHumanoid() )
				EditorGUILayout.HelpBox( "If dress has an animator and the avatar set for that animator is humanoid, you can use the dress up function even if the dress and avatar's bone transforms have different names.", MessageType.Info );

			if( !_GUILayout_RestoreDressBones() )
				EditorGUILayout.HelpBox( "If dress has an animator and the avatar set for that animator is humanoid, you can restore already installed bone transforms into dress. (Only after dressing up using the Alt function)", MessageType.Info );

			GUILayout.Space( 10f );
			GUILayout.Label( "---------- Copy and paste bounds value ----------" );
			EditorGUILayout.HelpBox( "If your dress is sometimes not visible from the edge of someone else's view, try this feature.", MessageType.None );
			_GUILayout_SkinnedMeshRendererBoundEdit();

			GUILayout.Space( 10f );
			GUILayout.Label( "---------- Set anchor override to spine bone ----------" );
			EditorGUILayout.HelpBox( "If your body and dress receive different light intensity, try this feature.", MessageType.None );
			_GUILayout_AnchorOverride();

			GUILayout.Space( 10f );
			_isIncludeInactive = GUILayout.Toggle( _isIncludeInactive, "Include inactive gameobjects" );
		}

		void _GUILayout_TransferBonesToTarget()
		{
			SkinnedMeshRenderer targetRenderer = null, dressRenderer = null;

			if( _go_Avatar != null )
			{
				targetRenderer = _go_Avatar.GetComponentInChildren<SkinnedMeshRenderer>();
				if( targetRenderer == null )
					GUILayout.Label( "Avatar does not have an SkinnedMeshRenderer.", _style_Error );
			}

			if( _go_Dress != null )
			{
				dressRenderer = _go_Dress.GetComponentInChildren<SkinnedMeshRenderer>(); ;
				if( dressRenderer == null )
					GUILayout.Label( "Dress does not have an SkinnedMeshRenderer.", _style_Error );
			}

			if( targetRenderer != null && dressRenderer != null )
			{
				if( GUILayout.Button( "Dress up!" ) )
				{
					var targetBones = targetRenderer.bones;
					foreach( Transform bone in dressRenderer.bones )
					{
						var targetBone = Array.Find( targetBones, v => { return v.name == bone.name; } );
						if( targetBone != null )
							_ChangeParent( bone, targetBone );
					}
				}
			}
		}

		bool _GUILayout_TransferBonesToTarget_BasedOnHumanoid()
		{
			if( _go_Avatar == null || _go_Dress == null )
				return false;

			Animator animator_Target = _go_Avatar.GetComponent<Animator>();
			Animator animator_Dress = _go_Dress.GetComponent<Animator>();
			bool isTargetHuman = false, isDressHuman = false;

			if( animator_Target != null )
				isTargetHuman = animator_Target.isHuman;

			if( animator_Dress != null )
				isDressHuman = animator_Dress.isHuman;

			if( isTargetHuman && isDressHuman )
			{
				if( GUILayout.Button( "Dress up! - Based on humanoid bones" ) )
				{
					for( int i = 0; i < (int)HumanBodyBones.LastBone; i++ )
					{
						var targetBone = animator_Target.GetBoneTransform( (HumanBodyBones)i );
						var dressBone = animator_Dress.GetBoneTransform( (HumanBodyBones)i );

						if( targetBone == null || dressBone == null )
							continue;

						_ChangeParent( dressBone, targetBone );
					}
				}
				return true;
			}

			return false;

		}

		bool _GUILayout_SelectDressBones()
		{
			if( _go_Dress == null )
				return false;

			SkinnedMeshRenderer renderer = _go_Dress.GetComponentInChildren<SkinnedMeshRenderer>(_isIncludeInactive);

			if( renderer == null )
				return false;

			if( GUILayout.Button( "Select all bone" ) )
			{
				var goList = new List<GameObject>();

				foreach( Transform bone in renderer.bones )
					goList.Add( bone.gameObject );

				Selection.objects = goList.ToArray();
			}

			return true;
		}

		bool _GUILayout_RestoreDressBones()
		{
			if( _go_Dress == null )
				return false;

			Animator animator = _go_Dress.GetComponent<Animator>();
			SkinnedMeshRenderer renderer = _go_Dress.GetComponentInChildren<SkinnedMeshRenderer>(_isIncludeInactive);

			if( animator == null || renderer == null )
				return false;

			if( !animator.isHuman )
				return false;

			if( GUILayout.Button( "Restore bones" ) )
			{
				BoneData[] bones = new BoneData[(int)HumanBodyBones.LastBone];

				bool isRootBoneInDress = renderer.rootBone.transform.root == _go_Dress.transform;
				HumanBone[] humanBones = animator.avatar.humanDescription.human;
				foreach( var bone in renderer.bones )
				{
					HumanBone humanBone = ArrayUtility.Find( humanBones, v => { return v.boneName == bone.name; } );
					if( !string.IsNullOrEmpty( humanBone.boneName ) )
					{
						HumanBodyBones type = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), Regex.Replace( humanBone.humanName, @"\s+", ""));
						bones[(int)type] = new BoneData( bone, type );
					}
				}

				bool isUpperChestExist = bones[(int)HumanBodyBones.UpperChest] != null;

				if( !isRootBoneInDress )
				{
					_ChangeParent( renderer.rootBone, _go_Dress.transform );
					renderer.rootBone.SetSiblingIndex( 0 );
				}

				if( renderer.rootBone != bones[(int)HumanBodyBones.Hips].transform )
					_ChangeParent( bones[(int)HumanBodyBones.Hips].transform, renderer.rootBone );

				foreach( var bone in bones )
				{
					if( bone == null )
						continue;

					HumanBodyBones parentBoneType = _GetParentBoneType( bone.type, isUpperChestExist );
					if( parentBoneType == HumanBodyBones.LastBone )
						continue;

					_ChangeParent( bone.transform, bones[(int)parentBoneType].transform );
				}

				foreach( var bone in renderer.bones )
				{
					if( bone.root == animator.transform.root )
						continue;

					if( bone.parent != null && bone.parent.name == bone.name )
					{
						if( bone.parent.parent != null )
						{
							string targetParentBoneName = bone.parent.parent.name;
							var targetParentBone = Array.Find(bones, v => { return v == null ? false : v.transform.name == targetParentBoneName; });
							if( targetParentBone != null )
								_ChangeParent( bone, targetParentBone.transform );
						}
					}

				}
			}

			return true;
		}

		bool _GUILayout_SkinnedMeshRendererBoundEdit()
		{
			if( _go_Avatar == null )
				return false;

			GUILayout.BeginHorizontal();
			GUILayout.Label( "Original renderer", GUILayout.Width( 120f ) );
			_go_OriginalSkinnedMeshRenderer = EditorGUILayout.ObjectField( _go_OriginalSkinnedMeshRenderer, typeof( GameObject ), true ) as GameObject;
			GUILayout.EndHorizontal();
			if( _go_OriginalSkinnedMeshRenderer == null )
				return false;

			SkinnedMeshRenderer renderer_Original = _go_OriginalSkinnedMeshRenderer.GetComponent<SkinnedMeshRenderer>();
			if( renderer_Original == null )
				return false;

			if( GUILayout.Button( "Paste bounds value" ) )
			{
				Bounds originalLocalBounds = renderer_Original.localBounds;
				foreach( SkinnedMeshRenderer renderer in _go_Avatar.GetComponentsInChildren<SkinnedMeshRenderer>( _isIncludeInactive ) )
				{
					if( renderer.GetHashCode() == renderer_Original.GetHashCode() )
						continue;

					Undo.RecordObject( renderer, "Paste bounds" );
					renderer.localBounds = originalLocalBounds;
				}
			}

			return true;
		}

		bool _GUILayout_AnchorOverride()
		{
			if( _go_Avatar == null )
				return false;

			Animator animator_Target = _go_Avatar.GetComponent<Animator>();
			bool isTargetHuman = false;

			if( animator_Target != null )
				isTargetHuman = animator_Target.isHuman;

			if( !isTargetHuman )
				return false;

			Transform transform_Spine = animator_Target.GetBoneTransform( HumanBodyBones.Spine );
			if( transform_Spine == null )
				return false;

			if( GUILayout.Button( "Set all anchor override" ) )
			{
				foreach( SkinnedMeshRenderer renderer in _go_Avatar.GetComponentsInChildren<SkinnedMeshRenderer>( _isIncludeInactive ) )
				{
					_SetAnchorOverride( transform_Spine, renderer );
				}
			}

			if( GUILayout.Button( "Set only selected objects anchor override" ) )
			{
				UnityEngine.Object[] objects = Selection.objects;
				for( int i = 0; i < objects.Length; i++ )
				{
					GameObject go = objects[i] as GameObject;
					if( go == null )
						continue;

					foreach( SkinnedMeshRenderer renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>( _isIncludeInactive ) )
					{
						_SetAnchorOverride( transform_Spine, renderer );
					}
				}
			}

			return true;
		}

		void _SetAnchorOverride( Transform anchor, SkinnedMeshRenderer target )
		{
			Undo.RecordObject( target, "Setting anchor override" );
			target.probeAnchor = anchor;
		}

		void _ChangeParent( Transform transform, Transform newParent )
		{
			Undo.SetTransformParent( transform, newParent, "Change parent" );
		}

		HumanBodyBones _GetParentBoneType( HumanBodyBones type, bool isUpperChestExist = false )
		{
			switch( type )
			{
			case HumanBodyBones.Hips: return HumanBodyBones.LastBone;
			case HumanBodyBones.LeftUpperLeg: return HumanBodyBones.Hips;
			case HumanBodyBones.RightUpperLeg: return HumanBodyBones.Hips;
			case HumanBodyBones.LeftLowerLeg: return HumanBodyBones.LeftUpperLeg;
			case HumanBodyBones.RightLowerLeg: return HumanBodyBones.RightUpperLeg;
			case HumanBodyBones.LeftFoot: return HumanBodyBones.LeftLowerLeg;
			case HumanBodyBones.RightFoot: return HumanBodyBones.RightLowerLeg;
			case HumanBodyBones.Spine: return HumanBodyBones.Hips;
			case HumanBodyBones.Chest: return HumanBodyBones.Hips;
			case HumanBodyBones.UpperChest: return HumanBodyBones.Chest;
			case HumanBodyBones.Neck: return isUpperChestExist ? HumanBodyBones.UpperChest : HumanBodyBones.Chest;
			case HumanBodyBones.Head: return HumanBodyBones.Neck;
			case HumanBodyBones.LeftShoulder: return isUpperChestExist ? HumanBodyBones.UpperChest : HumanBodyBones.Chest;
			case HumanBodyBones.RightShoulder: return isUpperChestExist ? HumanBodyBones.UpperChest : HumanBodyBones.Chest;
			case HumanBodyBones.LeftUpperArm: return HumanBodyBones.LeftShoulder;
			case HumanBodyBones.RightUpperArm: return HumanBodyBones.RightShoulder;
			case HumanBodyBones.LeftLowerArm: return HumanBodyBones.LeftUpperArm;
			case HumanBodyBones.RightLowerArm: return HumanBodyBones.RightUpperArm;
			case HumanBodyBones.LeftHand: return HumanBodyBones.LeftLowerArm;
			case HumanBodyBones.RightHand: return HumanBodyBones.RightLowerArm;
			case HumanBodyBones.LeftToes: return HumanBodyBones.LeftFoot;
			case HumanBodyBones.RightToes: return HumanBodyBones.RightFoot;
			case HumanBodyBones.LeftEye: return HumanBodyBones.Head;
			case HumanBodyBones.RightEye: return HumanBodyBones.Head;
			case HumanBodyBones.Jaw: return HumanBodyBones.Head;
			case HumanBodyBones.LeftThumbProximal: return HumanBodyBones.LeftHand;
			case HumanBodyBones.LeftThumbIntermediate: return HumanBodyBones.LeftThumbProximal;
			case HumanBodyBones.LeftThumbDistal: return HumanBodyBones.LeftThumbIntermediate;
			case HumanBodyBones.LeftIndexProximal: return HumanBodyBones.LeftHand;
			case HumanBodyBones.LeftIndexIntermediate: return HumanBodyBones.LeftIndexProximal;
			case HumanBodyBones.LeftIndexDistal: return HumanBodyBones.LeftIndexIntermediate;
			case HumanBodyBones.LeftMiddleProximal: return HumanBodyBones.LeftHand;
			case HumanBodyBones.LeftMiddleIntermediate: return HumanBodyBones.LeftMiddleProximal;
			case HumanBodyBones.LeftMiddleDistal: return HumanBodyBones.LeftMiddleIntermediate;
			case HumanBodyBones.LeftRingProximal: return HumanBodyBones.LeftHand;
			case HumanBodyBones.LeftRingIntermediate: return HumanBodyBones.LeftRingProximal;
			case HumanBodyBones.LeftRingDistal: return HumanBodyBones.LeftRingIntermediate;
			case HumanBodyBones.LeftLittleProximal: return HumanBodyBones.LeftHand;
			case HumanBodyBones.LeftLittleIntermediate: return HumanBodyBones.LeftLittleProximal;
			case HumanBodyBones.LeftLittleDistal: return HumanBodyBones.LeftLittleIntermediate;
			case HumanBodyBones.RightThumbProximal: return HumanBodyBones.RightHand;
			case HumanBodyBones.RightThumbIntermediate: return HumanBodyBones.RightThumbProximal;
			case HumanBodyBones.RightThumbDistal: return HumanBodyBones.RightThumbIntermediate;
			case HumanBodyBones.RightIndexProximal: return HumanBodyBones.RightHand;
			case HumanBodyBones.RightIndexIntermediate: return HumanBodyBones.RightIndexProximal;
			case HumanBodyBones.RightIndexDistal: return HumanBodyBones.RightIndexIntermediate;
			case HumanBodyBones.RightMiddleProximal: return HumanBodyBones.RightHand;
			case HumanBodyBones.RightMiddleIntermediate: return HumanBodyBones.RightMiddleProximal;
			case HumanBodyBones.RightMiddleDistal: return HumanBodyBones.RightMiddleIntermediate;
			case HumanBodyBones.RightRingProximal: return HumanBodyBones.RightHand;
			case HumanBodyBones.RightRingIntermediate: return HumanBodyBones.RightRingProximal;
			case HumanBodyBones.RightRingDistal: return HumanBodyBones.RightRingIntermediate;
			case HumanBodyBones.RightLittleProximal: return HumanBodyBones.RightHand;
			case HumanBodyBones.RightLittleIntermediate: return HumanBodyBones.RightLittleProximal;
			case HumanBodyBones.RightLittleDistal: return HumanBodyBones.RightLittleIntermediate;

			case HumanBodyBones.LastBone:
			default:
				return HumanBodyBones.LastBone;
			}
		}
	}
}
