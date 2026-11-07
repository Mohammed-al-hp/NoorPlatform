/// User model mirroring the .NET backend's login response `user` object.
class UserModel {
  final int id;
  final String fullName;
  final String phone;
  final String? email;
  final String role; // "Student" | "Parent"

  const UserModel({
    required this.id,
    required this.fullName,
    required this.phone,
    this.email,
    required this.role,
  });

  bool get isStudent => role == 'Student';
  bool get isParent => role == 'Parent';

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      id: json['id'] as int,
      fullName: json['fullName'] as String,
      phone: json['phone'] as String? ?? '',
      email: json['email'] as String?,
      role: json['role'] as String,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'fullName': fullName,
        'phone': phone,
        'email': email,
        'role': role,
      };
}

/// Auth response model mirroring POST /api/auth/login response.
class AuthResponseModel {
  final String token;
  final bool mustChangePassword;
  final UserModel user;

  const AuthResponseModel({
    required this.token,
    required this.mustChangePassword,
    required this.user,
  });

  factory AuthResponseModel.fromJson(Map<String, dynamic> json) {
    return AuthResponseModel(
      token: json['token'] as String,
      mustChangePassword: json['mustChangePassword'] as bool? ?? false,
      user: UserModel.fromJson(json['user'] as Map<String, dynamic>),
    );
  }
}

/// Login request payload for POST /api/auth/login.
class LoginRequestModel {
  final String phone;
  final String password;

  const LoginRequestModel({
    required this.phone,
    required this.password,
  });

  Map<String, dynamic> toJson() => {
        'phone': phone,
        'password': password,
      };
}

/// Change password request payload for POST /api/auth/change-password.
class ChangePasswordRequestModel {
  final String currentPassword;
  final String newPassword;

  const ChangePasswordRequestModel({
    required this.currentPassword,
    required this.newPassword,
  });

  Map<String, dynamic> toJson() => {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
      };
}
