import React from 'react'
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom'
import { LandingPage } from '../features/landing/pages/LandingPage'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { AuthProtected } from '../features/auth/components/AuthProtected'
import { AppLayout } from './layouts/AppLayout'
import { NewAnalysisPage } from '../features/analysis/pages/NewAnalysisPage'
import { AnalysisResultPage } from '../features/analysis/pages/AnalysisResultPage'
import { OptimizationConfirmationsPage } from '../features/optimization/pages/OptimizationConfirmationsPage'
import { OptimizationAdaptationPage } from '../features/optimization/pages/OptimizationAdaptationPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <LandingPage />,
  },
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/app',
    element: (
      <AuthProtected>
        <AppLayout />
      </AuthProtected>
    ),
    children: [
      {
        index: true,
        element: <Navigate to="/app/analises/nova" replace />,
      },
      {
        path: 'analises/nova',
        element: <NewAnalysisPage />,
      },
      {
        path: 'analises/:analysisId',
        element: <AnalysisResultPage />,
      },
      {
        path: 'adaptacoes/:optimizationId/confirmacoes',
        element: <OptimizationConfirmationsPage />,
      },
      {
        path: 'adaptacoes/:optimizationId',
        element: <OptimizationAdaptationPage />,
      },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
