using System;
using Bots.GridControl.DataTypes;
using VRageMath;

namespace Bots.GridControl.Controllers.WhipsCode
{
	internal static class RotationAngles
	{
		// Whip's Get Rotation Angles Method v18 - 05/09/20
		// Dependencies: VectorMath
		// Note: Set desiredUpVector to Vector3D.Zero if you don't care about roll
			
        internal static void GetRotationAnglesWithRoll(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, GyroOverrides overrides)
        {
            Vector3D localTargetVector = Vector3D.Rotate(desiredForwardVector, MatrixD.Transpose(worldMatrix));
            Vector3D flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

            int yawSign = localTargetVector.X >= 0 ? 1 : -1;
            overrides.SetYaw(VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * yawSign); //right is positive -- yaw

            int pitchSign = Math.Sign(localTargetVector.Y);
            if (Vector3D.IsZero(flattenedTargetVector))//check for straight up case
	            overrides.SetPitch(MathHelper.PiOver2 * pitchSign); // pitch
            else
	            overrides.SetPitch(VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * pitchSign); //up is positive

            if (Vector3D.IsZero(desiredUpVector))
            {
	            overrides.SetRoll(0); // roll
                return;
            }

            // Since there is a relationship between roll and the orientation of forward
            // we need to ensure that the up we are comparing is orthogonal to forward.
            Vector3D orthogonalLeft = Vector3D.Cross(desiredUpVector, desiredForwardVector);
            Vector3D orthogonalUp = Math.Abs(Vector3D.Dot(desiredForwardVector, desiredUpVector)) > 0 ? desiredUpVector : Vector3D.Cross(desiredForwardVector, orthogonalLeft);

            Vector3D localUpVector = Vector3D.Rotate(orthogonalUp, MatrixD.Transpose(worldMatrix));
            int signRoll = Vector3D.Dot(localUpVector, Vector3D.Right) >= 0 ? 1 : -1;

            // Desired forward and current up are parallel
            // This implies pitch is ±90° and yaw is 0°.
            if (Vector3D.IsZero(flattenedTargetVector))
            {
                Vector3D localUpFlattenedY = new Vector3D(localUpVector.X, 0, localUpVector.Z);

                // If straight up, reference direction would be backward,
                // if straight down, reference direction would be forward.
                // This is because we are simply doing a ±90° pitch rotation
                // of the axes.
                Vector3D referenceDirection = Vector3D.Dot(Vector3D.Up, localTargetVector) >= 0 ? Vector3D.Backward : Vector3D.Forward;

                overrides.SetRoll(VectorMath.AngleBetween(localUpFlattenedY, referenceDirection) * signRoll); // roll
                return;
            }

            // We are going to try and construct new intermediate axes where:
            // Up = Vector3D.Up
            // Front = flattenedTargetVector
            //
            // This will let us create a plane that contains Vector3D.Up and 
            // whose normal equals flattenedTargetVector
            Vector3D intermediateFront = flattenedTargetVector;

            // Reject up vector onto the plane normal
            Vector3D localUpProjOnIntermediateForward = Vector3D.Dot(intermediateFront, localUpVector) / intermediateFront.LengthSquared() * intermediateFront;
            Vector3D flattenedUpVector = localUpVector - localUpProjOnIntermediateForward;

            Vector3D intermediateRight = Vector3D.Cross(intermediateFront, Vector3D.Up);
            int rollSign = Vector3D.Dot(flattenedUpVector, intermediateRight) >= 0 ? 1 : -1;
            overrides.SetRoll(VectorMath.AngleBetween(flattenedUpVector, Vector3D.Up) * rollSign); // roll
        }

		internal static void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, GyroOverrides overrides)
		{
			MatrixD transposedWm;
			MatrixD.Transpose(ref worldMatrix, out transposedWm);
			Vector3D.Rotate(ref desiredForwardVector, ref transposedWm, out desiredForwardVector);
			Vector3D.Rotate(ref desiredUpVector, ref transposedWm, out desiredUpVector);

			Vector3D leftVector = Vector3D.Cross(desiredUpVector, desiredForwardVector);
			Vector3D axis;
			double angle;
			if (Vector3D.IsZero(desiredUpVector) || Vector3D.IsZero(leftVector))
			{
				desiredForwardVector = VectorMath.SafeNormalize(desiredForwardVector);
				axis = Vector3D.Cross(Vector3D.Forward, desiredForwardVector);
				angle = Math.Asin(axis.Length());
			}
			else
			{
				leftVector = VectorMath.SafeNormalize(leftVector);
				Vector3D upVector = Vector3D.Cross(desiredForwardVector, leftVector);

				// Create matrix
				MatrixD targetMatrix = MatrixD.Zero;
				targetMatrix.Forward = desiredForwardVector;
				targetMatrix.Left = leftVector;
				targetMatrix.Up = upVector;

				axis = Vector3D.Cross(Vector3D.Backward, targetMatrix.Backward)
				       + Vector3D.Cross(Vector3D.Up, targetMatrix.Up)
				       + Vector3D.Cross(Vector3D.Right, targetMatrix.Right);

				double trace = targetMatrix.M11 + targetMatrix.M22 + targetMatrix.M33;
				angle = Math.Acos((trace - 1) * 0.5);
			}

			axis = VectorMath.SafeNormalize(axis);
			overrides.SetYaw(-axis.Y * angle);
			overrides.SetPitch(axis.X * angle);
			overrides.SetRoll(- axis.Z * angle);
		}
    }
}
