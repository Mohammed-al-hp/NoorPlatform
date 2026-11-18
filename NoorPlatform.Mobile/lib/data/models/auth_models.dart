/// User model mirroring the .NET backend's login response `user` object.
class UserModel {
  final int id;
  final String fullName;
  final String phone;
  final String role; // "Admin" | "Teacher" | "Student" | "Parent"
  /// تسمية الدور بالعربية من الخادم (مدير عام / مشرف / محفّظ / طالب / ولي أمر).
  final String? serverRoleLabel;
  final bool isSuperAdmin;

  const UserModel({
    required this.id,
    required this.fullName,
    required this.phone,
    required this.role,
    this.serverRoleLabel,
    this.isSuperAdmin = false,
  });

  bool get isStudent => role == 'Student';
  bool get isParent => role == 'Parent';

  /// لا تظهر Admin/Teacher بالإنجليزية في الواجهة أبداً.
  String get roleLabel {
    if (serverRoleLabel != null && serverRoleLabel!.isNotEmpty) {
      return serverRoleLabel!;
    }
    switch (role) {
      case 'Admin':
        return isSuperAdmin ? 'مدير عام' : 'مشرف';
      case 'Teacher':
        return 'محفّظ';
      case 'Student':
        return 'طالب';
      case 'Parent':
        return 'ولي أمر';
      default:
        return role;
    }
  }

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      id: json['id'] as int,
      fullName: json['fullName'] as String,
      phone: json['phone'] as String? ?? '',
      role: json['role'] as String,
      serverRoleLabel: json['roleLabel'] as String?,
      isSuperAdmin: json['isSuperAdmin'] == true,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'fullName': fullName,
        'phone': phone,
        'role': role,
        'roleLabel': serverRoleLabel,
        'isSuperAdmin': isSuperAdmin,
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
