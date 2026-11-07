import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/storage/app_preferences.dart';

class ThemeCubit extends Cubit<ThemeMode> {
  final AppPreferences preferences;

  ThemeCubit(this.preferences) : super(ThemeMode.system);

  Future<void> load() async {
    emit(await preferences.getThemeMode());
  }

  Future<void> setThemeMode(ThemeMode mode) async {
    await preferences.setThemeMode(mode);
    emit(mode);
  }
}
